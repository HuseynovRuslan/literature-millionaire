using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LiteratureMillionaire.Tests;

/// <summary>An admin client for panel tests, signed in the real way: QRLog confirmation, ticket, session.</summary>
internal static class AdminTestClient
{
    public const string Secret = "test-shared-secret-value";
    public const string AdminPhone = "+994505551234";

    public static LeaderboardApiFactory NewFactory() => new(qrLogSecret: Secret, adminPhones: AdminPhone);

    /// <summary>The session cookie is Secure: over plain http the client would, correctly, never send it back.</summary>
    public static HttpClient Https(LeaderboardApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    /// <summary>A state-changing request, with the admin header.</summary>
    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, object body)
    {
        var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(AdminAuth.CsrfHeader, "1");
        return client.SendAsync(request);
    }

    public static async Task<HttpClient> SignedInAsync(LeaderboardApiFactory factory)
    {
        var client = Https(factory);
        using var start = await client.PostAsync("/api/qrlog-login/start", null);
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        var code = started.GetProperty("code").GetString()!;
        var pollSecret = started.GetProperty("pollSecret").GetString()!;

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{code}\n{AdminPhone}\n{timestamp}")));
        using var confirm = await client.PostAsJsonAsync("/api/qrlog-login/confirm",
            new { code, fullName = "Admin İşçi", phoneNumber = AdminPhone, timestamp, signature });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        using var poll = await client.GetAsync($"/api/qrlog-login/{code}?secret={pollSecret}");
        var ticket = (await poll.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("signInTicket").GetString()!;
        using var session = await SendAsync(client, HttpMethod.Post, "/api/admin/session", new { signInTicket = ticket });
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        return client;
    }
}
