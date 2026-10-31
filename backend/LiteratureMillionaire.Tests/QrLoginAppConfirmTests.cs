using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// The phone route into a sign-in: a screen cannot be scanned by the very phone showing it, so the start
/// response can carry an address where QRLog approves that same code. It is configuration, and configuration
/// that sends a live sign-in code somewhere unintended is refused rather than trusted.
/// </summary>
public class QrLoginAppConfirmTests
{
    [Fact]
    public async Task The_configured_address_comes_back_with_the_code_in_it()
    {
        await using var factory = new LeaderboardApiFactory(qrLogAppConfirmUrl: "https://app.qrlog.az/kitabxana?code={code}");
        using var client = factory.CreateClient();

        using var started = await client.PostAsync("/api/qrlog-login/start", null);
        var body = await started.Content.ReadFromJsonAsync<JsonElement>();

        var code = body.GetProperty("code").GetString()!;
        var url = body.GetProperty("appConfirmUrl").GetString();
        Assert.Equal($"https://app.qrlog.az/kitabxana?code={code}", url);
    }

    [Fact]
    public async Task Without_configuration_there_is_no_address_and_the_screen_shows_only_the_qr()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();

        using var started = await client.PostAsync("/api/qrlog-login/start", null);

        var appConfirmUrl = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appConfirmUrl");
        Assert.Equal(JsonValueKind.Null, appConfirmUrl.ValueKind);
    }

    [Theory]
    // Somewhere else entirely, plain http, a host that only looks like QRLog, and one with no code in it.
    [InlineData("https://evil.example.com/confirm?code={code}")]
    [InlineData("http://app.qrlog.az/kitabxana?code={code}")]
    [InlineData("https://qrlog.az.evil.example.com/?code={code}")]
    [InlineData("https://app.qrlog.az/kitabxana")]
    [InlineData("not a url at all")]
    public async Task An_address_that_is_not_an_https_qrlog_one_with_a_code_is_ignored(string configured)
    {
        await using var factory = new LeaderboardApiFactory(qrLogAppConfirmUrl: configured);
        using var client = factory.CreateClient();

        using var started = await client.PostAsync("/api/qrlog-login/start", null);

        var appConfirmUrl = (await started.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("appConfirmUrl");
        Assert.Equal(JsonValueKind.Null, appConfirmUrl.ValueKind);
        Assert.Contains(factory.Logs, line => line.Contains("QrLog:AppConfirmUrl is ignored"));
    }
}

/// <summary>
/// A sign-in has to survive the browser handing the person to a different window: QRLog gives them back to an
/// installed app or a fresh tab, which has none of the original page's storage. The server keeps it in a cookie
/// every window of the browser shares.
/// </summary>
public class QrLoginResumeTests
{
    [Fact]
    public async Task Another_window_of_the_same_browser_picks_up_the_sign_in_and_a_stranger_cannot()
    {
        await using var factory = new LeaderboardApiFactory(qrLogSecret: "test-shared-secret-value");
        using var browser = AdminTestClient.Https(factory);

        using var started = await browser.PostAsync("/api/qrlog-login/start", null);
        var first = await started.Content.ReadFromJsonAsync<JsonElement>();
        var cookie = Assert.Single(started.Headers.GetValues("Set-Cookie"), c => c.StartsWith("kitabxana_qr=", StringComparison.Ordinal)).ToLowerInvariant();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("path=/api/qrlog-login", cookie);

        // The same browser, a different window: the same code, the same secret, the same time left.
        using var resumed = await browser.GetAsync("/api/qrlog-login/resume");
        Assert.Equal(HttpStatusCode.OK, resumed.StatusCode);
        var again = await resumed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("code").GetString(), again.GetProperty("code").GetString());
        Assert.Equal(first.GetProperty("pollSecret").GetString(), again.GetProperty("pollSecret").GetString());
        Assert.InRange(again.GetProperty("secondsToLive").GetInt32(), 1, 300);

        // Somebody else's browser has no cookie and gets nothing - not even a hint that a sign-in exists.
        using var stranger = AdminTestClient.Https(factory);
        using var nothing = await stranger.GetAsync("/api/qrlog-login/resume");
        Assert.Equal(HttpStatusCode.NoContent, nothing.StatusCode);
    }
}
