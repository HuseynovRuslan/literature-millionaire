using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Admin panel access (phase 1b): who gets a session, how it is protected, and that everything admins do is
/// recorded. The session cookie is Secure, so these tests talk to the host over https - over http the client
/// would, correctly, never send it back.
/// </summary>
public class AdminSessionTests
{
    private const string Secret = "test-shared-secret-value";
    private const string AdminPhone = "+994505551234";
    private const string AdminName = "Admin İşçi";
    private const string OtherPhone = "+994557770000";

    [Fact]
    public async Task Without_a_session_every_admin_endpoint_answers_401()
    {
        await using var factory = NewFactory();
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();

        using var session = await client.GetAsync("/api/admin/session");
        using var audit = await client.GetAsync("/api/admin/audit");
        using var questions = await client.GetAsync("/api/questions");

        Assert.Equal(HttpStatusCode.Unauthorized, session.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, audit.StatusCode);
        // The old unauthenticated question CRUD is behind the admin policy now too.
        Assert.Equal(HttpStatusCode.Unauthorized, questions.StatusCode);
    }

    [Fact]
    public async Task An_admin_signs_in_with_the_qrlog_ticket_and_the_session_lasts_until_sign_out()
    {
        await using var factory = NewFactory();
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();

        using var signIn = await PostAdminAsync(client, "/api/admin/session", new { signInTicket = await TicketAsync(client, AdminPhone) });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // The cookie is what makes it a session worth trusting: unreadable to scripts, never over plain http,
        // never sent by another site.
        var cookie = Assert.Single(signIn.Headers.GetValues("Set-Cookie"), c => c.StartsWith("kitabxana_admin=", StringComparison.Ordinal))
            .ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);

        using var current = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var me = await current.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(AdminName, me.GetProperty("fullName").GetString());
        // Shown masked: the screen needs to tell colleagues apart, not to give out their numbers.
        Assert.Equal("+994 50 *** ** 34", me.GetProperty("phone").GetString());

        using var audit = await client.GetAsync("/api/admin/audit");
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        var entries = await audit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(entries.EnumerateArray(), e => e.GetProperty("action").GetString() == "sign-in");
        Assert.DoesNotContain(AdminPhone, entries.GetRawText());

        using var signOut = await DeleteAdminAsync(client, "/api/admin/session");
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        using var after = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Someone_not_on_the_admin_list_is_refused_and_the_attempt_is_recorded()
    {
        await using var factory = NewFactory();
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();

        using var signIn = await PostAdminAsync(client, "/api/admin/session", new { signInTicket = await TicketAsync(client, OtherPhone) });

        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
        Assert.Equal("NOT_AN_ADMIN", (await signIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.False(signIn.Headers.TryGetValues("Set-Cookie", out _));
        using var current = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.Unauthorized, current.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var denied = Assert.Single(await db.AdminAuditEntries.AsNoTracking().ToListAsync());
        Assert.Equal("sign-in-denied", denied.Action);
        Assert.Equal(OtherPhone, denied.ActorPhone);
    }

    [Fact]
    public async Task With_no_admin_list_configured_nobody_is_an_admin()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, adminPhones: null);
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();

        using var signIn = await PostAdminAsync(client, "/api/admin/session", new { signInTicket = await TicketAsync(client, AdminPhone) });

        Assert.Equal(HttpStatusCode.Forbidden, signIn.StatusCode);
    }

    [Fact]
    public async Task A_ticket_opens_one_admin_session_and_no_more()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var first = Https(factory);
        using var second = Https(factory);
        var ticket = await TicketAsync(first, AdminPhone);

        using var once = await PostAdminAsync(first, "/api/admin/session", new { signInTicket = ticket });
        using var twice = await PostAdminAsync(second, "/api/admin/session", new { signInTicket = ticket });

        Assert.Equal(HttpStatusCode.OK, once.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, twice.StatusCode);
    }

    [Fact]
    public async Task A_state_changing_admin_request_without_the_admin_header_is_refused_even_with_a_session()
    {
        await using var factory = NewFactory();
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();
        using var signIn = await PostAdminAsync(client, "/api/admin/session", new { signInTicket = await TicketAsync(client, AdminPhone) });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        // What a forged cross-site request looks like: the cookie may ride along, the header cannot.
        using var forged = await client.DeleteAsync("/api/admin/session");

        Assert.Equal(HttpStatusCode.BadRequest, forged.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await forged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var stillSignedIn = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.OK, stillSignedIn.StatusCode);
    }

    [Fact]
    public async Task A_break_glass_link_signs_in_once_and_not_after_it_expires()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();

        string token, expiredToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IAdminLoginLinks>();
            token = await links.CreateAsync(AdminPhone, "Təcili giriş");
            expiredToken = await links.CreateAsync(AdminPhone, "Təcili giriş");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Only the hash is stored: the table cannot sign anybody in.
            Assert.DoesNotContain(await db.AdminLoginLinks.Select(l => l.TokenHash).ToListAsync(), h => h == token);
            var newest = await db.AdminLoginLinks.OrderByDescending(l => l.Id).FirstAsync();
            newest.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using var client = Https(factory);
        using var used = await PostAdminAsync(client, "/api/admin/session/link", new { token });
        using var again = await PostAdminAsync(Https(factory), "/api/admin/session/link", new { token });
        using var expired = await PostAdminAsync(Https(factory), "/api/admin/session/link", new { token = expiredToken });

        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var current = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Fact]
    public async Task Taking_someone_off_the_admin_list_ends_their_access_on_the_next_request()
    {
        // The list is re-read by the policy on every request, not only at sign-in: an eight-hour cookie must not
        // outlive the decision to remove somebody.
        await using var factory = NewFactory();
        using var client = Https(factory);
        await factory.CreateDatabaseAsync();
        using var signIn = await PostAdminAsync(client, "/api/admin/session", new { signInTicket = await TicketAsync(client, AdminPhone) });
        using var before = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        factory.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>()["Admin:Phones"] = "0501112233";

        using var after = await client.GetAsync("/api/admin/session");
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
        using var audit = await client.GetAsync("/api/admin/audit");
        Assert.Equal(HttpStatusCode.Forbidden, audit.StatusCode);
    }

    // --- helpers ---------------------------------------------------------------

    private static LeaderboardApiFactory NewFactory() =>
        new(qrLogSecret: Secret, adminPhones: $"{AdminPhone}, 0501112233");

    private static HttpClient Https(LeaderboardApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static Task<HttpResponseMessage> PostAdminAsync(HttpClient client, string url, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(AdminAuth.CsrfHeader, "1");
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> DeleteAdminAsync(HttpClient client, string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Add(AdminAuth.CsrfHeader, "1");
        return client.SendAsync(request);
    }

    /// <summary>A real QRLog sign-in for <paramref name="phone"/>: open, confirm with a signature, poll for the ticket.</summary>
    private static async Task<string> TicketAsync(HttpClient client, string phone)
    {
        using var start = await client.PostAsync("/api/qrlog-login/start", null);
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        var code = started.GetProperty("code").GetString()!;
        var pollSecret = started.GetProperty("pollSecret").GetString()!;

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{code}\n{phone}\n{timestamp}")));
        using var confirm = await client.PostAsJsonAsync("/api/qrlog-login/confirm",
            new { code, fullName = AdminName, phoneNumber = phone, timestamp, signature });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using var poll = await client.GetAsync($"/api/qrlog-login/{code}?secret={pollSecret}");
        return (await poll.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("signInTicket").GetString()!;
    }
}
