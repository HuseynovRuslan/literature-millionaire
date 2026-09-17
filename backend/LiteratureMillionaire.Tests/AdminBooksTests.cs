using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Books in the admin panel (phase 5): adding the month's book with its cover, and the two things that must
/// not happen - a cover pointing anywhere but this site, and a book being switched off out from under a
/// campaign people are playing.
/// </summary>
public class AdminBooksTests
{
    [Fact]
    public async Task Books_need_an_admin_session_and_changes_need_the_admin_header()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var anonymous = Https(factory);

        using var list = await anonymous.GetAsync("/api/admin/books");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsJsonAsync("/api/admin/books", Input("Kitab", "Müəllif"));
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_admin_adds_the_months_book_with_a_cover_and_it_is_recorded()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);
        var cover = "/uploads/covers/" + new string('a', 32) + ".webp";

        using var created = await SendAsync(admin, HttpMethod.Post, "/api/admin/books",
            Input("Ölülər", "Cəlil Məmmədquluzadə", description: "1909-cu ildə yazılmış tragikomediya.", cover: cover));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(cover, body.GetProperty("coverImageUrl").GetString());
        Assert.Equal(0, body.GetProperty("questionCount").GetInt32());
        Assert.Equal(0, body.GetProperty("campaignCount").GetInt32());

        var listed = Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/api/admin/books")).EnumerateArray());
        Assert.Equal("Ölülər", listed.GetProperty("title").GetString());
        Assert.True(listed.GetProperty("isActive").GetBoolean());

        var entry = await LastAuditAsync(factory);
        Assert.Equal("book-created", entry.Action);
        Assert.Contains("Ölülər", entry.Details);

        // The same title twice would leave two books nobody can tell apart on any screen.
        using var again = await SendAsync(admin, HttpMethod.Post, "/api/admin/books", Input("Ölülər", "Başqa müəllif"));
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Contains("artıq var", (await Errors(again)).GetProperty("title")[0].GetString());
    }

    [Fact]
    public async Task A_cover_must_be_a_picture_from_this_site()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);

        foreach (var cover in new[]
                 {
                     "https://example.com/cover.webp",
                     "//example.com/cover.webp",
                     "/uploads/covers/../../etc/passwd",
                     "/uploads/questions/" + new string('a', 32) + ".webp", // a question picture, not a cover
                     "javascript:alert(1)",
                 })
        {
            using var refused = await SendAsync(admin, HttpMethod.Post, "/api/admin/books", Input("Kitab " + cover.Length, "Müəllif", cover: cover));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
            Assert.True((await Errors(refused)).TryGetProperty("coverImageUrl", out _));
        }

        // The two that are allowed: a shipped cover, and none at all.
        using var shipped = await SendAsync(admin, HttpMethod.Post, "/api/admin/books", Input("Şəkilli", "Müəllif", cover: "/covers/oluler.webp"));
        using var none = await SendAsync(admin, HttpMethod.Post, "/api/admin/books", Input("Şəkilsiz", "Müəllif", cover: ""));
        Assert.Equal(HttpStatusCode.Created, shipped.StatusCode);
        Assert.Equal(HttpStatusCode.Created, none.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(2, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Books.CountAsync());
    }

    [Fact]
    public async Task A_book_in_a_running_campaign_cannot_be_switched_off_by_accident()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var admin = await SignedInAsync(factory);
        var book = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/books")).EnumerateArray().Single();
        var id = book.GetProperty("id").GetInt32();

        // It says what is in the way, and names it.
        Assert.Equal(10, book.GetProperty("questionCount").GetInt32());
        Assert.Equal(1, book.GetProperty("campaignCount").GetInt32());
        Assert.Equal(campaignId, book.GetProperty("campaigns")[0].GetProperty("id").GetInt32());

        using var off = await SendAsync(admin, HttpMethod.Put, $"/api/admin/books/{id}", Input("Test Book", "Test Author", active: false));
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
        Assert.Contains($"#{campaignId}", (await Errors(off)).GetProperty("isActive")[0].GetString());

        // Renaming it is fine, and lands where players read it.
        using var renamed = await SendAsync(admin, HttpMethod.Put, $"/api/admin/books/{id}", Input("Ayın kitabı: Ölülər", "Cəlil Məmmədquluzadə"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);
        using var player = Https(factory);
        var campaign = await player.GetFromJsonAsync<JsonElement>("/api/campaigns/available");
        Assert.Equal("Ayın kitabı: Ölülər", campaign[0].GetProperty("book").GetProperty("title").GetString());

        var entry = await LastAuditAsync(factory);
        Assert.Equal("book-updated", entry.Action);
        Assert.Contains("Ad: \"Test Book\" → \"Ayın kitabı: Ölülər\"", entry.Details);

        using var missing = await SendAsync(admin, HttpMethod.Put, "/api/admin/books/9999", Input("Yox", "Yox"));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_book_with_no_campaign_can_be_switched_off()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        using var admin = await SignedInAsync(factory);
        using var created = await SendAsync(admin, HttpMethod.Post, "/api/admin/books", Input("Köhnə kitab", "Müəllif"));
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        using var off = await SendAsync(admin, HttpMethod.Put, $"/api/admin/books/{id}", Input("Köhnə kitab", "Müəllif", active: false));

        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False((await off.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isActive").GetBoolean());
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Switched off, never deleted: what was played from it has to stay readable.
        Assert.Equal(1, await db.Books.CountAsync());
    }

    // --- helpers ---------------------------------------------------------------

    private static object Input(string title, string author, string description = "", string cover = "", bool active = true) => new
    {
        title,
        author,
        description,
        coverImageUrl = cover,
        isActive = active,
    };

    private static async Task<JsonElement> Errors(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");

    private static async Task<AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }
}
