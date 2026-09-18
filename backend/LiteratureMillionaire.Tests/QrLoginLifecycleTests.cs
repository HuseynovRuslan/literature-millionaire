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
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// What ends a QRLog sign-in.
///
/// Every one of these was a way to get stuck, reported from a phone: signing out of the panel put the sign-in
/// screen in a loop, the game screen went dead after an admin sign-in, and the next person on a shared device
/// arrived already signed in as the last one. One cause behind all three - a sign-in had no end. It stayed
/// alive for its five minutes, the cookie kept pointing at it, and every later screen resumed it.
/// </summary>
public class QrLoginLifecycleTests
{
    private const string Secret = "test-shared-secret-value";
    private const string Phone = "+994505551234";

    [Fact]
    public async Task Signing_out_of_the_panel_ends_the_sign_in_so_the_next_qr_is_a_new_one()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var browser = Https(factory);

        var first = await SignInAsync(browser, Phone);
        using var session = await SendAsync(browser, HttpMethod.Post, "/api/admin/session", new { signInTicket = first.Ticket });
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        using var signOut = await SendAsync(browser, HttpMethod.Delete, "/api/admin/session", null);
        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);

        // Nothing is left to resume, so the screen that comes next mints a fresh code instead of being handed
        // the spent one over and over.
        using var resumed = await browser.GetAsync("/api/qrlog-login/resume");
        Assert.Equal(HttpStatusCode.NoContent, resumed.StatusCode);
        using var polled = await browser.GetAsync($"/api/qrlog-login/{first.Code}?secret={first.Secret}");
        Assert.Equal(HttpStatusCode.NotFound, polled.StatusCode);

        // And signing in again works, first time.
        var second = await SignInAsync(browser, Phone);
        Assert.NotEqual(first.Code, second.Code);
        using var again = await SendAsync(browser, HttpMethod.Post, "/api/admin/session", new { signInTicket = second.Ticket });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task An_admin_session_takes_its_sign_in_with_it_so_the_game_screen_is_not_handed_a_dead_ticket()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var browser = Https(factory);

        var signIn = await SignInAsync(browser, Phone);
        using var session = await SendAsync(browser, HttpMethod.Post, "/api/admin/session", new { signInTicket = signIn.Ticket });
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);

        // The game screen on the same phone gets nothing to resume - it used to receive this sign-in, hand its
        // spent ticket to the start endpoint and fail on every press of "Yarışa başla".
        using var resumed = await browser.GetAsync("/api/qrlog-login/resume");
        Assert.Equal(HttpStatusCode.NoContent, resumed.StatusCode);
    }

    [Fact]
    public async Task Starting_a_quiz_ends_the_sign_in_so_the_next_participant_gets_their_own()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var browser = Https(factory);

        var player = await SignInAsync(browser, "+994550000101", "Birinci Oyunçu");
        using var started = await browser.PostAsJsonAsync("/api/game/start", new { campaignId, signInTicket = player.Ticket });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);

        // Handing the phone on: nothing of the last player is left for the next screen to pick up.
        using var resumed = await browser.GetAsync("/api/qrlog-login/resume");
        Assert.Equal(HttpStatusCode.NoContent, resumed.StatusCode);
        using var polled = await browser.GetAsync($"/api/qrlog-login/{player.Code}?secret={player.Secret}");
        Assert.Equal(HttpStatusCode.NotFound, polled.StatusCode);

        // The next person signs in as themselves, on their own code.
        var next = await SignInAsync(browser, "+994550000102", "İkinci Oyunçu");
        Assert.NotEqual(player.Code, next.Code);
        using var theirGame = await browser.PostAsJsonAsync("/api/game/start", new { campaignId, signInTicket = next.Ticket });
        Assert.Equal(HttpStatusCode.OK, theirGame.StatusCode);

        // Two quizzes, two people - not one person playing twice under somebody else's name.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var players = await db.QuizAttempts.Include(a => a.Participant)
            .OrderBy(a => a.Id).Select(a => a.Participant.FullName).ToListAsync();
        Assert.Equal(new[] { "Birinci Oyunçu", "İkinci Oyunçu" }, players);
    }

    [Fact]
    public async Task Asking_for_a_new_qr_really_gives_a_new_one()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var browser = Https(factory);

        var first = await SignInAsync(browser, Phone);

        // "Yeni QR kod" - the escape hatch that could not escape: starting a sign-in used to leave the old one
        // alive, so the screen resumed it right back.
        using var startedAgain = await browser.PostAsync("/api/qrlog-login/start", null);
        var second = await startedAgain.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(first.Code, second.GetProperty("code").GetString());
        using var polledOld = await browser.GetAsync($"/api/qrlog-login/{first.Code}?secret={first.Secret}");
        Assert.Equal(HttpStatusCode.NotFound, polledOld.StatusCode);
    }

    [Fact]
    public async Task A_person_can_end_their_own_sign_in_and_only_their_own()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var browser = Https(factory);
        using var stranger = Https(factory);

        var signIn = await SignInAsync(browser, Phone);

        // Somebody else's browser holds no cookie for it, so its attempt to end the sign-in touches nothing.
        using var strangerEnds = await SendAsync(stranger, HttpMethod.Delete, "/api/qrlog-login/pending", null);
        Assert.Equal(HttpStatusCode.NoContent, strangerEnds.StatusCode);
        using var stillThere = await browser.GetAsync($"/api/qrlog-login/{signIn.Code}?secret={signIn.Secret}");
        Assert.Equal(HttpStatusCode.OK, stillThere.StatusCode);

        // The browser that started it can end it, and the ticket dies with it.
        using var ended = await SendAsync(browser, HttpMethod.Delete, "/api/qrlog-login/pending", null);
        Assert.Equal(HttpStatusCode.NoContent, ended.StatusCode);
        using var gone = await browser.GetAsync($"/api/qrlog-login/{signIn.Code}?secret={signIn.Secret}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        using var withDeadTicket = await SendAsync(browser, HttpMethod.Post, "/api/admin/session", new { signInTicket = signIn.Ticket });
        Assert.Equal(HttpStatusCode.Unauthorized, withDeadTicket.StatusCode);
    }

    [Fact]
    public async Task A_refused_admin_sign_in_does_not_cost_a_player_their_sign_in()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var browser = Https(factory);

        // A player - not on the admin list - whose browser ends up at /admin.
        var player = await SignInAsync(browser, "+994550000101", "Oyunçu");
        using var refused = await SendAsync(browser, HttpMethod.Post, "/api/admin/session", new { signInTicket = player.Ticket });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // Their sign-in is untouched: being turned away from a door they were never meant to open must not
        // spend the one attempt they signed in for.
        using var started = await browser.PostAsJsonAsync("/api/game/start", new { campaignId, signInTicket = player.Ticket });
        Assert.Equal(HttpStatusCode.OK, started.StatusCode);
    }

    // --- helpers ---------------------------------------------------------------

    private static LeaderboardApiFactory NewFactory() => new(qrLogSecret: Secret, adminPhones: Phone, requireQrLogin: true);

    private static HttpClient Https(LeaderboardApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private sealed record SignedIn(string Code, string Secret, string Ticket);

    /// <summary>A whole sign-in as a phone does it: open, QRLog confirms with its signature, poll for the ticket.</summary>
    private static async Task<SignedIn> SignInAsync(HttpClient client, string phone, string name = "Test İstifadəçi")
    {
        using var start = await client.PostAsync("/api/qrlog-login/start", null);
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        var code = started.GetProperty("code").GetString()!;
        var pollSecret = started.GetProperty("pollSecret").GetString()!;

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{code}\n{phone}\n{timestamp}")));
        using var confirm = await client.PostAsJsonAsync("/api/qrlog-login/confirm",
            new { code, fullName = name, phoneNumber = phone, timestamp, signature });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using var poll = await client.GetAsync($"/api/qrlog-login/{code}?secret={pollSecret}");
        var status = await poll.Content.ReadFromJsonAsync<JsonElement>();
        return new SignedIn(code, pollSecret, status.GetProperty("signInTicket").GetString()!);
    }
}
