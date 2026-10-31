using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// "QRLog ilə davam et". This is the one place where something outside the quiz can say who a player
/// is, so the tests are about what must NOT be possible: vouching without the shared secret, replaying
/// a captured confirmation, reading an identity off a QR someone photographed, or reusing a code.
/// </summary>
public class QrLoginApiTests
{
    private const string Secret = "test-shared-secret-value";
    private const string Phone = "+994505551234";
    private const string Name = "Ayşə Məmmədova";

    private static string Sign(string code, string phone, string timestamp, string secret = Secret) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{code}\n{phone}\n{timestamp}")));

    private static string Now() => DateTimeOffset.UtcNow.ToString("O");

    private static async Task<(string code, string secret)> StartAsync(HttpClient client)
    {
        using var response = await client.PostAsync("/api/qrlog-login/start", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (body.GetProperty("code").GetString()!, body.GetProperty("pollSecret").GetString()!);
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string code, string signature, string timestamp, string phone = Phone) =>
        client.PostAsJsonAsync("/api/qrlog-login/confirm", new { code, fullName = Name, phoneNumber = phone, timestamp, signature });

    [Fact]
    public async Task A_signed_confirmation_reaches_every_copy_of_the_screen_that_is_waiting()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, pollSecret) = await StartAsync(client);

        // Before QRLog says anything, the kiosk is simply waiting - and learns nothing.
        var waiting = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("pending", waiting.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, waiting.GetProperty("phoneNumber").ValueKind);

        var timestamp = Now();
        using var confirm = await ConfirmAsync(client, code, Sign(code, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        var confirmed = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(Name, confirmed.GetProperty("fullName").GetString());
        Assert.Equal(Phone, confirmed.GetProperty("phoneNumber").GetString());

        // Asked again - by the same screen after a reload, or by the second tab QRLog handed the browser
        // back to - it answers the same thing, with the same ticket. Delivering it once meant whichever copy
        // asked first took the sign-in and the other was told its code had expired.
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("confirmed", second.GetProperty("status").GetString());
        Assert.Equal(confirmed.GetProperty("signInTicket").GetString(), second.GetProperty("signInTicket").GetString());

        // The secret is still what proves it is that browser asking.
        using var stranger = await client.GetAsync($"/api/qrlog-login/{code}?secret=0123456789abcdef0123456789abcdef");
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
    }

    [Fact]
    public async Task Nobody_without_the_shared_secret_can_vouch_for_an_employee()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, pollSecret) = await StartAsync(client);
        var timestamp = Now();

        // Signed with the wrong secret, and signed for a different phone than the one submitted.
        using var wrongSecret = await ConfirmAsync(client, code, Sign(code, Phone, timestamp, "not-the-secret"), timestamp);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);

        using var swappedPhone = await ConfirmAsync(client, code, Sign(code, "+994500000000", timestamp), timestamp);
        Assert.Equal(HttpStatusCode.Unauthorized, swappedPhone.StatusCode);

        using var noSignature = await ConfirmAsync(client, code, new string('a', 64), timestamp);
        Assert.Equal(HttpStatusCode.Unauthorized, noSignature.StatusCode);

        // None of that moved the login on.
        var still = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("pending", still.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_captured_confirmation_cannot_be_replayed_later()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, _) = await StartAsync(client);

        var old = DateTimeOffset.UtcNow.AddMinutes(-30).ToString("O");
        using var stale = await ConfirmAsync(client, code, Sign(code, Phone, old), old);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
    }

    [Fact]
    public async Task A_photographed_qr_is_useless_without_the_secret_the_kiosk_kept()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, pollSecret) = await StartAsync(client);
        var timestamp = Now();
        using var confirm = await ConfirmAsync(client, code, Sign(code, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        // Someone who only has what the QR showed cannot read the employee off it.
        foreach (var attempt in new[] { $"/api/qrlog-login/{code}", $"/api/qrlog-login/{code}?secret=", $"/api/qrlog-login/{code}?secret=guess" })
        {
            using var response = await client.GetAsync(attempt);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain(Phone, await response.Content.ReadAsStringAsync());
        }

        // The real kiosk is unaffected by those attempts.
        var mine = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("confirmed", mine.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_code_can_only_be_confirmed_once_and_an_unknown_code_is_refused()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, _) = await StartAsync(client);
        var timestamp = Now();

        using var first = await ConfirmAsync(client, code, Sign(code, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var second = await ConfirmAsync(client, code, Sign(code, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var unknown = "00000000000000000000000000000000";
        using var never = await ConfirmAsync(client, unknown, Sign(unknown, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.Conflict, never.StatusCode);
    }

    /// <summary>
    /// The quiz files a player under "+994XXXXXXXXX" and its one-attempt rule is keyed on that, so a
    /// sign-in has to arrive in a form it can use. It did not, the first time this shipped: QRLog sent
    /// the nine national digits its own column holds, the confirmation was accepted, the kiosk showed a
    /// name, and the start button then failed with nothing on screen to explain it. Refusing at the door
    /// is the difference between an integrator seeing the problem and a player seeing a dead button.
    /// </summary>
    [Theory]
    [InlineData("501234567")]          // nine national digits, as QRLog stores them
    [InlineData("1234567")]            // right length, not a number this quiz knows
    [InlineData("0121234567")]         // a landline prefix
    [InlineData("not a phone at all")]
    public async Task A_phone_the_quiz_cannot_use_is_refused_at_the_door(string phone)
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, pollSecret) = await StartAsync(client);
        var timestamp = Now();

        using var response = await ConfirmAsync(client, code, Sign(code, phone, timestamp), timestamp, phone);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("INVALID_PHONE", await response.Content.ReadAsStringAsync());

        // And the kiosk is left waiting rather than handed something it cannot start a quiz with.
        var still = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("pending", still.GetProperty("status").GetString());
    }

    /// <summary>
    /// However it is spelled, the kiosk receives the one form the quiz uses - so somebody who signed in
    /// with QRLog and somebody who once typed the same number by hand are the same participant, and the
    /// one attempt per campaign still means one.
    /// </summary>
    [Theory]
    [InlineData("+994501234567")]
    [InlineData("0501234567")]
    [InlineData("994 50 123 45 67")]
    [InlineData("050-123-45-67")]
    public async Task Every_accepted_spelling_reaches_the_kiosk_as_the_same_number(string phone)
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        var (code, pollSecret) = await StartAsync(client);
        var timestamp = Now();

        using var response = await ConfirmAsync(client, code, Sign(code, phone, timestamp), timestamp, phone);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var confirmed = await client.GetFromJsonAsync<JsonElement>($"/api/qrlog-login/{code}?secret={pollSecret}");
        Assert.Equal("+994501234567", confirmed.GetProperty("phoneNumber").GetString());
    }

    [Fact]
    public async Task With_no_secret_configured_the_feature_refuses_rather_than_trusts()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var (code, _) = await StartAsync(client);
        var timestamp = Now();

        using var response = await ConfirmAsync(client, code, Sign(code, Phone, timestamp), timestamp);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_code_in_the_qr_never_carries_the_poll_secret()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: Secret);
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/api/qrlog-login/start", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var code = body.GetProperty("code").GetString()!;
        var pollSecret = body.GetProperty("pollSecret").GetString()!;
        Assert.DoesNotContain(pollSecret, code, StringComparison.OrdinalIgnoreCase);
        // Both are full-length random hex, not something a bystander could guess from the other.
        Assert.Equal(32, code.Length);
        Assert.Equal(32, pollSecret.Length);
        Assert.Matches("^[0-9a-f]+$", code);
    }
}
