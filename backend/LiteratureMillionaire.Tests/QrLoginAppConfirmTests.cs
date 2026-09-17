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
