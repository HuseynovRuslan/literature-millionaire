using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// A quiz can only be started as the person QRLog vouched for.
///
/// Before this, the start endpoint took any name and phone it was sent: the QRLog sign-in on the page could
/// be walked around with one HTTP request, as anyone, as many times as one had phone numbers - in a
/// competition with a prize. These tests are the proof that the gap is closed, not just the page.
/// </summary>
public class QrLoginTicketTests
{
    private const string Secret = "test-shared-secret-value";
    private const string Phone = "+994505551234";
    private const string Name = "Ayşə Məmmədova";

    [Fact]
    public async Task A_bare_name_and_phone_cannot_start_a_quiz_when_sign_in_is_required()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, requireQrLogin: true);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        using var response = await client.PostAsJsonAsync("/api/game/start",
            new { fullName = Name, phoneNumber = Phone, campaignId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("SIGN_IN_REQUIRED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        // Refused before anything was written: no participant, no spent attempt for the phone it named.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.Participants.CountAsync());
        Assert.Equal(0, await db.QuizAttempts.CountAsync());
    }

    [Fact]
    public async Task A_made_up_ticket_is_refused_as_an_expired_sign_in()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, requireQrLogin: true);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        using var response = await client.PostAsJsonAsync("/api/game/start",
            new { signInTicket = new string('a', 64), campaignId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("SIGN_IN_EXPIRED", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_confirmed_sign_in_starts_the_quiz_as_the_vouched_for_employee_whatever_the_body_claims()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, requireQrLogin: true);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        var ticket = await SignInAsync(client);

        // The body names somebody else. The ticket decides, and the body is ignored.
        using var response = await client.PostAsJsonAsync("/api/game/start",
            new { signInTicket = ticket, fullName = "Başqa Adam", phoneNumber = "+994500000000", campaignId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = Assert.Single(await db.Participants.AsNoTracking().ToListAsync());
        Assert.Equal(Phone, participant.NormalizedPhoneNumber);
        Assert.Equal(Name, participant.FullName);
    }

    [Fact]
    public async Task The_ticket_is_only_handed_out_once_the_sign_in_is_confirmed()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, requireQrLogin: true);
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();

        var (code, secret) = await StartLoginAsync(client);
        var pending = await PollAsync(client, code, secret);
        Assert.Equal("pending", pending.GetProperty("status").GetString());
        Assert.True(pending.GetProperty("signInTicket").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined);

        await ConfirmAsync(client, code);
        var confirmed = await PollAsync(client, code, secret);
        Assert.Equal("confirmed", confirmed.GetProperty("status").GetString());
        // As strong as a session key, and never the code or the poll secret reused.
        var ticket = confirmed.GetProperty("signInTicket").GetString()!;
        Assert.Matches("^[0-9a-f]{64}$", ticket);
        Assert.NotEqual(code, ticket);
        Assert.NotEqual(secret, ticket);
    }

    [Fact]
    public async Task A_ticket_still_cannot_buy_a_second_attempt()
    {
        // A ticket is not consumed, so it can start another category within its lifetime - but only ever as
        // the same person, and the one-attempt rule still stands for them.
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret, requireQrLogin: true);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var ticket = await SignInAsync(client);

        using var first = await client.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });
        using var second = await client.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ATTEMPT_LIMIT_REACHED", (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    // --- helpers ---------------------------------------------------------------

    private static async Task<string> SignInAsync(HttpClient client)
    {
        var (code, secret) = await StartLoginAsync(client);
        await ConfirmAsync(client, code);
        var confirmed = await PollAsync(client, code, secret);
        return confirmed.GetProperty("signInTicket").GetString()!;
    }

    private static async Task<(string code, string secret)> StartLoginAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/qrlog-login/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("code").GetString()!, body.GetProperty("pollSecret").GetString()!);
    }

    private static async Task ConfirmAsync(HttpClient client, string code)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{code}\n{Phone}\n{timestamp}")));
        using var response = await client.PostAsJsonAsync("/api/qrlog-login/confirm",
            new { code, fullName = Name, phoneNumber = Phone, timestamp, signature });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<JsonElement> PollAsync(HttpClient client, string code, string secret)
    {
        using var response = await client.GetAsync($"/api/qrlog-login/{code}?secret={secret}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
