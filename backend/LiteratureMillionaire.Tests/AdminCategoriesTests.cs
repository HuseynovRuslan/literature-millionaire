using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Categories in the admin panel (phase 8): the tiles on the home page. What matters is that the things players
/// read - the title, the order, the "test version" label - can be changed without a deployment, and that a
/// category cannot be taken off the home page while people are playing it.
/// </summary>
public class AdminCategoriesTests
{
    [Fact]
    public async Task Categories_need_an_admin_session_and_changes_need_the_admin_header()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var anonymous = Https(factory);

        using var list = await anonymous.GetAsync("/api/admin/categories");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PutAsJsonAsync("/api/admin/categories/1", Input("Yeni ad"));
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_category_is_listed_with_what_depends_on_it_and_renaming_it_reaches_players()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var admin = await SignedInAsync(factory);

        var categories = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories")).EnumerateArray().ToList();
        var bilik = categories.Single(c => c.GetProperty("slug").GetString() == QuizModeSlugs.BilikDunyasi);
        Assert.Equal(10, bilik.GetProperty("questionCount").GetInt32());
        Assert.Equal(1, bilik.GetProperty("campaignCount").GetInt32());
        Assert.True(bilik.GetProperty("hasRunningCampaign").GetBoolean());
        // "Ayın Kitabı" is the one played per book; the panel says so rather than leaving it to be learned.
        Assert.True(categories.Single(c => c.GetProperty("slug").GetString() == QuizModeSlugs.AyinKitabi).GetProperty("requiresBook").GetBoolean());

        var id = bilik.GetProperty("id").GetInt32();
        using var renamed = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}",
            Input("Bilik Aləmi", description: "Yeni təsvir", icon: "book"));
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        using var player = Https(factory);
        var available = await player.GetFromJsonAsync<JsonElement>("/api/campaigns/available");
        var mode = available[0].GetProperty("quizMode");
        Assert.Equal(("Bilik Aləmi", "Yeni təsvir", "book"), (mode.GetProperty("title").GetString(),
            mode.GetProperty("description").GetString(), mode.GetProperty("iconKey").GetString()));

        var entry = await LastAuditAsync(factory);
        Assert.Equal("category-updated", entry.Action);
        Assert.Contains("Ad: \"Bilik Dünyası\" → \"Bilik Aləmi\"", entry.Details);
    }

    [Fact]
    public async Task The_test_version_label_is_the_categorys_own_flag_and_can_be_taken_off()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var admin = await SignedInAsync(factory);
        var bilik = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories")).EnumerateArray()
            .Single(c => c.GetProperty("slug").GetString() == QuizModeSlugs.BilikDunyasi);
        var id = bilik.GetProperty("id").GetInt32();
        Assert.False(bilik.GetProperty("isPreview").GetBoolean());

        // Put the label on: a bank being worked on says so to players.
        using var marked = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}", Input("Bilik Dünyası", preview: true));
        Assert.True((await marked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isPreview").GetBoolean());

        using var player = Https(factory);
        var available = await player.GetFromJsonAsync<JsonElement>("/api/campaigns/available");
        Assert.True(available[0].GetProperty("quizMode").GetProperty("isPreview").GetBoolean());

        // And off again, which used to take a deployment.
        using var cleared = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}", Input("Bilik Dünyası"));
        Assert.False((await cleared.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isPreview").GetBoolean());
        Assert.Contains("Test versiya: bəli → xeyr", (await LastAuditAsync(factory)).Details);
    }

    [Fact]
    public async Task A_category_people_are_playing_cannot_be_switched_off_and_an_unknown_icon_is_refused()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var admin = await SignedInAsync(factory);
        var id = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories")).EnumerateArray()
            .Single(c => c.GetProperty("slug").GetString() == QuizModeSlugs.BilikDunyasi).GetProperty("id").GetInt32();

        using var off = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}", Input("Bilik Dünyası", active: false));
        Assert.Equal(HttpStatusCode.BadRequest, off.StatusCode);
        Assert.Contains($"#{campaignId}", (await Errors(off)).GetProperty("isActive")[0].GetString());

        // An icon the frontend cannot draw would render as a generic glyph, which reads as a bug.
        using var badIcon = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}", Input("Bilik Dünyası", icon: "rocket"));
        Assert.True((await Errors(badIcon)).TryGetProperty("iconKey", out _));

        using var blank = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{id}", Input("  "));
        Assert.True((await Errors(blank)).TryGetProperty("title", out _));

        // A category nobody is playing can be switched off, and its campaigns leave the home page with it.
        var idle = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories")).EnumerateArray()
            .Single(c => c.GetProperty("slug").GetString() == QuizModeSlugs.YasilBaki).GetProperty("id").GetInt32();
        using var idleOff = await SendAsync(admin, HttpMethod.Put, $"/api/admin/categories/{idle}", Input("Yaşıl Bakı", active: false));
        Assert.Equal(HttpStatusCode.OK, idleOff.StatusCode);
    }

    [Fact]
    public async Task Moving_a_category_changes_the_order_players_see()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await QuizModeSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }
        using var admin = await SignedInAsync(factory);

        var before = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories")).EnumerateArray()
            .Select(c => c.GetProperty("slug").GetString()).ToList();
        var second = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories"))[1].GetProperty("id").GetInt32();

        using var moved = await SendAsync(admin, HttpMethod.Post, $"/api/admin/categories/{second}/move?direction=up", null);
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var reordered = await moved.Content.ReadFromJsonAsync<JsonElement>();
        var after = reordered.EnumerateArray().Select(c => c.GetProperty("slug").GetString()).ToList();
        Assert.Equal(before[1], after[0]);
        Assert.Equal(before[0], after[1]);
        // Renumbered from one, so the list says exactly what the home page shows.
        Assert.Equal([1, 2, 3, 4, 5], reordered.EnumerateArray().Select(c => c.GetProperty("displayOrder").GetInt32()));

        // The one at the top has nowhere to go, and says so by changing nothing.
        var first = (await admin.GetFromJsonAsync<JsonElement>("/api/admin/categories"))[0].GetProperty("id").GetInt32();
        using var stays = await SendAsync(admin, HttpMethod.Post, $"/api/admin/categories/{first}/move?direction=up", null);
        Assert.Equal(after, (await stays.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()
            .Select(c => c.GetProperty("slug").GetString()).ToList());

        using var missing = await SendAsync(admin, HttpMethod.Post, "/api/admin/categories/9999/move?direction=down", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // --- helpers ---------------------------------------------------------------

    private static object Input(string title, string description = "Təsvir", string icon = "globe",
        bool active = true, bool preview = false) => new
    {
        title,
        description,
        iconKey = icon,
        isActive = active,
        isPreview = preview,
    };

    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CATEGORY_INVALID", body.GetProperty("code").GetString());
        return body.GetProperty("errors");
    }

    private static async Task<AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }
}
