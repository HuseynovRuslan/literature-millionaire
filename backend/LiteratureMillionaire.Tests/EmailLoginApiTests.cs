using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// "Email ilə daxil ol". Like the QRLog tests, these are mostly about what must NOT be possible: playing as an
/// address one cannot read, guessing a code, flooding somebody's inbox, getting into the admin panel by email, or
/// an address ending up in the log.
/// </summary>
public class EmailLoginApiTests
{
    private const string Name = "Leyla Əliyeva";
    private const string Typed = "  Leyla.Aliyeva@Mail.AZ ";
    private const string Address = "leyla.aliyeva@mail.az";

    [Fact]
    public async Task Without_a_mailbox_email_sign_in_is_off_and_sends_nothing()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();

        var status = await client.GetFromJsonAsync<JsonElement>("/api/email-login");
        Assert.False(status.GetProperty("enabled").GetBoolean());

        using var start = await StartAsync(client);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, start.StatusCode);
        Assert.Equal("EMAIL_LOGIN_DISABLED", await CodeOf(start));
    }

    [Fact]
    public async Task A_mailed_code_starts_the_quiz_as_that_address_whatever_the_body_claims()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(requireQrLogin: true, emailSender: mailbox);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        Assert.True((await client.GetFromJsonAsync<JsonElement>("/api/email-login")).GetProperty("enabled").GetBoolean());

        using var start = await StartAsync(client);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Address, started.GetProperty("email").GetString());
        Assert.Matches("^[0-9a-f]{32}$", started.GetProperty("loginId").GetString()!);

        // The code went to the normalised address, and only there; it is not in the response.
        var mail = Assert.Single(mailbox.Sent);
        Assert.Equal(Address, mail.To);
        Assert.Contains(Name, mail.TextBody);
        Assert.DoesNotContain(mailbox.LastCode, await start.Content.ReadAsStringAsync());

        using var verify = await VerifyAsync(client, started.GetProperty("loginId").GetString()!, mailbox.LastCode);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var verified = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(Name, verified.GetProperty("fullName").GetString());
        Assert.Equal(Address, verified.GetProperty("email").GetString());
        var ticket = verified.GetProperty("signInTicket").GetString()!;
        Assert.Matches("^[0-9a-f]{64}$", ticket);

        // The body names somebody else. The ticket decides.
        using var game = await client.PostAsJsonAsync("/api/game/start",
            new { signInTicket = ticket, fullName = "Başqa Adam", phoneNumber = "+994500000000", campaignId });
        Assert.Equal(HttpStatusCode.OK, game.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var participant = Assert.Single(await db.Participants.AsNoTracking().ToListAsync());
        Assert.Equal(Address, participant.Email);
        Assert.Null(participant.NormalizedPhoneNumber);
        Assert.Equal(Name, participant.FullName);

        // An address is personal data: it never reaches the log, not even through an exception message.
        Assert.DoesNotContain(factory.Logs, line => line.Contains(Address, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task One_address_is_one_attempt()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(requireQrLogin: true, emailSender: mailbox);
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var ticket = await SignInAsync(client, mailbox);

        using var first = await client.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });
        using var second = await client.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("ATTEMPT_LIMIT_REACHED", await CodeOf(second));
    }

    [Fact]
    public async Task A_code_dies_after_five_wrong_tries_and_cannot_be_used_after_that()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(emailSender: mailbox);
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();
        var loginId = await StartedLoginIdAsync(client);
        var wrong = mailbox.LastCode == "000000" ? "111111" : "000000";

        for (var left = EmailLoginService.MaxWrongCodes - 1; left >= 1; left--)
        {
            using var attempt = await VerifyAsync(client, loginId, wrong);
            Assert.Equal(HttpStatusCode.BadRequest, attempt.StatusCode);
            var body = await attempt.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("CODE_INVALID", body.GetProperty("code").GetString());
            Assert.Equal(left, body.GetProperty("attemptsLeft").GetInt32());
        }

        using var last = await VerifyAsync(client, loginId, wrong);
        Assert.Equal(HttpStatusCode.TooManyRequests, last.StatusCode);
        Assert.Equal("TOO_MANY_ATTEMPTS", await CodeOf(last));

        // The right code is no use now: guessing cannot be continued, only a new mail starts over.
        using var right = await VerifyAsync(client, loginId, mailbox.LastCode);
        Assert.Equal(HttpStatusCode.NotFound, right.StatusCode);
        Assert.Equal("LOGIN_NOT_FOUND", await CodeOf(right));
    }

    [Fact]
    public async Task A_code_works_once_and_only_with_the_login_it_was_sent_for()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(emailSender: mailbox);
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();
        var loginId = await StartedLoginIdAsync(client);

        // The code read over somebody's shoulder, without the id this browser holds, is nothing.
        using var stranger = await VerifyAsync(client, new string('a', 32), mailbox.LastCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);

        using var first = await VerifyAsync(client, loginId, mailbox.LastCode);
        using var again = await VerifyAsync(client, loginId, mailbox.LastCode);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Asking_again_within_a_minute_is_refused_with_the_wait()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(emailSender: mailbox);
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();

        using var first = await StartAsync(client);
        using var second = await StartAsync(client, email: Address.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EMAIL_CODE_COOLDOWN", body.GetProperty("code").GetString());
        Assert.InRange(body.GetProperty("retryAfterSeconds").GetInt32(), 1, 60);
        Assert.NotNull(second.Headers.RetryAfter);
        // The same address however it is typed: still one mail.
        Assert.Single(mailbox.Sent);
    }

    [Fact]
    public async Task A_bad_address_or_a_missing_name_is_refused_before_anything_is_sent()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(emailSender: mailbox);
        using var client = factory.CreateClient();

        using var badEmail = await StartAsync(client, email: "leyla@mail");
        using var noName = await StartAsync(client, name: " ");

        Assert.Equal(HttpStatusCode.BadRequest, badEmail.StatusCode);
        Assert.Equal("INVALID_EMAIL", await CodeOf(badEmail));
        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal("INVALID_NAME", await CodeOf(noName));
        Assert.Empty(mailbox.Sent);
    }

    [Fact]
    public async Task A_failed_send_leaves_no_code_behind_and_may_be_retried_at_once()
    {
        var mailbox = new FakeMailbox { FailuresLeft = 1 };
        await using var factory = new LeaderboardApiFactory(emailSender: mailbox);
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();

        using var failed = await StartAsync(client);
        Assert.Equal(HttpStatusCode.BadGateway, failed.StatusCode);
        Assert.Equal("EMAIL_SEND_FAILED", await CodeOf(failed));

        using var retry = await StartAsync(client);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Single(mailbox.Sent);
        // The failing server's message quoted the recipient; the log must not.
        Assert.DoesNotContain(factory.Logs, line => line.Contains(Address, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task An_email_sign_in_does_not_open_the_admin_panel_and_is_not_spent_trying()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(qrLogSecret: AdminTestClient.Secret,
            adminPhones: AdminTestClient.AdminPhone, emailSender: mailbox);
        using var client = AdminTestClient.Https(factory);
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var ticket = await SignInAsync(client, mailbox);

        using var session = await AdminTestClient.SendAsync(client, HttpMethod.Post, "/api/admin/session", new { signInTicket = ticket });
        Assert.Equal(HttpStatusCode.Forbidden, session.StatusCode);
        Assert.Equal("QRLOG_SIGN_IN_REQUIRED", await CodeOf(session));

        using var game = await client.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });
        Assert.Equal(HttpStatusCode.OK, game.StatusCode);
    }

    [Fact]
    public async Task The_admin_results_show_an_email_player_masked_and_find_them_by_address()
    {
        var mailbox = new FakeMailbox();
        await using var factory = new LeaderboardApiFactory(qrLogSecret: AdminTestClient.Secret,
            adminPhones: AdminTestClient.AdminPhone, emailSender: mailbox);
        using var player = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var ticket = await SignInAsync(player, mailbox);
        using var game = await player.PostAsJsonAsync("/api/game/start", new { signInTicket = ticket, campaignId });
        Assert.Equal(HttpStatusCode.OK, game.StatusCode);

        using var admin = await AdminTestClient.SignedInAsync(factory);
        var results = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/campaigns/{campaignId}/results?search=aliyeva@");
        var row = Assert.Single(results.GetProperty("attempts").EnumerateArray());
        Assert.Equal(Name, row.GetProperty("fullName").GetString());
        Assert.Equal("l***@mail.az", row.GetProperty("phone").GetString());

        using var export = await admin.GetAsync($"/api/admin/campaigns/{campaignId}/results.xlsx");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
    }

    // --- helpers ---------------------------------------------------------------

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string name = Name, string email = Typed) =>
        client.PostAsJsonAsync("/api/email-login/start", new { fullName = name, email });

    private static Task<HttpResponseMessage> VerifyAsync(HttpClient client, string loginId, string code) =>
        client.PostAsJsonAsync("/api/email-login/verify", new { loginId, code });

    private static async Task<string> StartedLoginIdAsync(HttpClient client)
    {
        using var start = await StartAsync(client);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        return (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("loginId").GetString()!;
    }

    private static async Task<string> SignInAsync(HttpClient client, FakeMailbox mailbox)
    {
        var loginId = await StartedLoginIdAsync(client);
        using var verify = await VerifyAsync(client, loginId, mailbox.LastCode);
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        return (await verify.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("signInTicket").GetString()!;
    }

    private static async Task<string?> CodeOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}

/// <summary>A mailbox that keeps what it is sent, and can be told to fail the way a real mail server does.</summary>
internal sealed class FakeMailbox : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    /// <summary>How many sends to fail before delivering again.</summary>
    public int FailuresLeft { get; set; }

    public bool IsConfigured => true;

    /// <summary>The six-digit code in the latest message.</summary>
    public string LastCode => Regex.Match(Sent.Last().TextBody, @"\b\d{6}\b").Value;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        if (FailuresLeft > 0)
        {
            FailuresLeft--;
            // Real SMTP errors quote the recipient, which is why the service never logs the message.
            throw new SmtpException($"Mailbox unavailable: {message.To}");
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
