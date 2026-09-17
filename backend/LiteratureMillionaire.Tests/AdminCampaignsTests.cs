using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Campaigns in the admin panel (phase 2): an administrator can open next month's campaign without a developer,
/// and cannot save one that players would find broken - no round to build, no book, or two at once.
/// </summary>
public class AdminCampaignsTests
{
    private static readonly DateOnly Today = CampaignCalendar.Today();

    [Fact]
    public async Task Campaigns_need_an_admin_session_and_changes_need_the_admin_header()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var anonymous = Https(factory);

        using var list = await anonymous.GetAsync("/api/admin/campaigns");
        using var options = await anonymous.GetAsync("/api/admin/campaigns/options");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, options.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsJsonAsync("/api/admin/campaigns", new { });
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task An_admin_opens_next_months_campaign_and_it_is_recorded()
    {
        await using var factory = NewFactory();
        var current = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (modeId, bookId) = await ModeAndBookAsync(factory, current);
        using var admin = await SignedInAsync(factory);

        var start = Today.AddDays(2);
        using var created = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns",
            Input(modeId, bookId, start, start.AddDays(29), reward: "Oktyabr hədiyyəsi"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("scheduled", body.GetProperty("status").GetString());
        Assert.Empty(body.GetProperty("issues").EnumerateArray());

        var campaigns = await ListAsync(admin);
        Assert.Equal(2, campaigns.Count);
        // Running first: the campaign people are playing is the one an admin looks for.
        Assert.Equal(current, campaigns[0].GetProperty("id").GetInt32());
        Assert.Equal("running", campaigns[0].GetProperty("status").GetString());

        var entry = await LastAuditAsync(factory);
        Assert.Equal("campaign-created", entry.Action);
        Assert.Equal(body.GetProperty("id").GetInt32().ToString(), entry.EntityId);
        Assert.Contains("Oktyabr", await admin.GetStringAsync("/api/admin/campaigns"));
    }

    [Fact]
    public async Task A_campaign_players_would_find_broken_is_refused_while_a_switched_off_draft_is_kept_with_its_problems()
    {
        await using var factory = NewFactory();
        var current = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (_, bookId) = await ModeAndBookAsync(factory, current);
        var emptyMode = await ModeIdAsync(factory, QuizModeSlugs.EdebiyyatDunyasi);
        using var admin = await SignedInAsync(factory);
        var start = Today.AddDays(2);

        // Nothing in this category: no round could be built.
        using var enabled = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(emptyMode, null, start, start.AddDays(10)));
        Assert.Equal(HttpStatusCode.BadRequest, enabled.StatusCode);
        var problem = await enabled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CAMPAIGN_INVALID", problem.GetProperty("code").GetString());
        Assert.Contains("Sual bankı raundu doldurmur", problem.GetProperty("errors").GetProperty("quizModeId")[0].GetString());

        using var draft = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(emptyMode, null, start, start.AddDays(10), enabled: false));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var saved = await draft.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("disabled", saved.GetProperty("status").GetString());
        Assert.Contains(saved.GetProperty("issues").EnumerateArray(), i => i.GetString()!.Contains("asan 0/3"));

        // "Ayın Kitabı" is played per book.
        var ayin = await ModeIdAsync(factory, QuizModeSlugs.AyinKitabi);
        using var bookless = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(ayin, null, start, start.AddDays(10), enabled: false));
        Assert.Equal(HttpStatusCode.BadRequest, bookless.StatusCode);
        Assert.True((await bookless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("bookId", out _));

        // Dates: the wrong way round, too long, or already over.
        var (modeId, _) = await ModeAndBookAsync(factory, current);
        using var backwards = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(modeId, bookId, start, start.AddDays(-1), enabled: false));
        using var tooLong = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(modeId, bookId, start, start.AddYears(36), enabled: false));
        using var over = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(modeId, bookId, Today.AddDays(-20), Today.AddDays(-10), enabled: false));
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);

        Assert.Equal(2, (await ListAsync(admin)).Count); // the seeded one and the draft, nothing else
    }

    [Fact]
    public async Task Two_enabled_campaigns_of_one_category_may_not_share_a_day_but_different_categories_may()
    {
        await using var factory = NewFactory();
        var current = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (modeId, bookId) = await ModeAndBookAsync(factory, current);
        using var admin = await SignedInAsync(factory);

        using var clash = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(modeId, bookId, Today.AddDays(1), Today.AddDays(20)));
        Assert.Equal(HttpStatusCode.BadRequest, clash.StatusCode);
        Assert.Contains($"#{current}", (await clash.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("startDate")[0].GetString());

        // Ending the running one the day before is exactly how a month hands over to the next.
        using var shortened = await SendAsync(admin, HttpMethod.Put, $"/api/admin/campaigns/{current}", Input(modeId, bookId, Today.AddDays(-1), Today));
        Assert.Equal(HttpStatusCode.OK, shortened.StatusCode);
        using var next = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(modeId, bookId, Today.AddDays(1), Today.AddDays(20)));
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);

        // Another category's questions and campaign, on the same days: allowed.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var yasil = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.YasilBaki);
            foreach (var q in await db.Questions.AsNoTracking().Where(q => q.BookId == bookId).ToListAsync())
            {
                db.Questions.Add(new Question
                {
                    BookId = null, QuizModeId = yasil, Text = q.Text + " (yaşıl)", OptionA = "A", OptionB = "B", OptionC = "C", OptionD = "D",
                    CorrectOption = 'A', Difficulty = q.Difficulty, Category = "Test",
                });
            }
            await db.SaveChangesAsync();
            using var parallel = await SendAsync(admin, HttpMethod.Post, "/api/admin/campaigns", Input(yasil, null, Today.AddDays(1), Today.AddDays(20)));
            Assert.Equal(HttpStatusCode.Created, parallel.StatusCode);
        }
    }

    [Fact]
    public async Task An_edit_is_what_players_get_and_the_trail_says_what_changed()
    {
        await using var factory = NewFactory();
        var current = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (modeId, bookId) = await ModeAndBookAsync(factory, current);
        using var admin = await SignedInAsync(factory);
        using var player = Https(factory);

        using var before = await player.GetAsync("/api/campaigns/available");
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        using var off = await SendAsync(admin, HttpMethod.Put, $"/api/admin/campaigns/{current}",
            Input(modeId, bookId, Today.AddDays(-1), Today.AddDays(1), reward: "Test Reward", passingScore: 7, enabled: false, images: QuizRules.DefaultImageQuestionsPerQuiz));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        using var after = await player.GetAsync("/api/campaigns/available");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);

        var entry = await LastAuditAsync(factory);
        Assert.Equal("campaign-updated", entry.Action);
        Assert.Equal("Aktiv: bəli → xeyr", entry.Details);

        using var missing = await SendAsync(admin, HttpMethod.Put, "/api/admin/campaigns/9999", Input(modeId, bookId, Today, Today));
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task A_campaign_with_attempts_keeps_its_category_and_book()
    {
        await using var factory = NewFactory();
        var current = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (modeId, bookId) = await ModeAndBookAsync(factory, current);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var participant = new Participant { FullName = "Test İştirakçı", NormalizedPhoneNumber = "+994550000101" };
            db.QuizAttempts.Add(new QuizAttempt
            {
                Participant = participant, CampaignId = current, AttemptNumber = 1, StartedAtUtc = DateTime.UtcNow,
                TotalQuestions = 10, PassingScore = 7, MaxPoints = 20,
            });
            await db.SaveChangesAsync();
        }
        var otherMode = await ModeIdAsync(factory, QuizModeSlugs.EdebiyyatDunyasi);
        using var admin = await SignedInAsync(factory);

        using var moved = await SendAsync(admin, HttpMethod.Put, $"/api/admin/campaigns/{current}",
            Input(otherMode, bookId, Today.AddDays(-1), Today.AddDays(1), enabled: false));
        Assert.Equal(HttpStatusCode.BadRequest, moved.StatusCode);
        Assert.Contains("1 cəhd", (await moved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("quizModeId")[0].GetString());

        // Dates and the reward may still change.
        using var extended = await SendAsync(admin, HttpMethod.Put, $"/api/admin/campaigns/{current}",
            Input(modeId, bookId, Today.AddDays(-1), Today.AddDays(30), reward: "Test Reward", passingScore: 7));
        Assert.Equal(HttpStatusCode.OK, extended.StatusCode);
        Assert.Equal(1, (await extended.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attemptsStarted").GetInt32());
    }

    [Fact]
    public async Task A_seeded_campaign_whose_dates_were_changed_in_the_panel_is_not_seeded_again()
    {
        await using var factory = NewFactory();
        await factory.CreateDatabaseAsync();
        await SeedAsync(factory);

        int count;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            count = await db.MonthlyCampaigns.CountAsync();
            Assert.True(count > 0);
            // What "next month" in the panel does to every seeded campaign.
            foreach (var campaign in await db.MonthlyCampaigns.ToListAsync())
            {
                campaign.StartDate = new DateOnly(2026, 10, 1);
                campaign.EndDate = new DateOnly(2026, 10, 31);
            }
            await db.SaveChangesAsync();
        }

        await SeedAsync(factory); // the next deployment

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(count, await db.MonthlyCampaigns.CountAsync());
            Assert.All(await db.MonthlyCampaigns.ToListAsync(), c => Assert.Equal(new DateOnly(2026, 10, 1), c.StartDate));
        }
    }

    // --- helpers ---------------------------------------------------------------

    private static object Input(int modeId, int? bookId, DateOnly start, DateOnly end, string reward = "Hədiyyə",
        int passingScore = 8, bool enabled = true, int images = 0) => new
    {
        quizModeId = modeId,
        bookId,
        startDate = start,
        endDate = end,
        passingScore,
        rewardTitle = reward,
        imageQuestionsPerQuiz = images,
        isEnabled = enabled,
    };

    private static async Task<List<JsonElement>> ListAsync(HttpClient admin) =>
        (await admin.GetFromJsonAsync<JsonElement>("/api/admin/campaigns")).EnumerateArray().ToList();

    private static async Task SeedAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<(int ModeId, int BookId)> ModeAndBookAsync(LeaderboardApiFactory factory, int campaignId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campaign = await db.MonthlyCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        return (campaign.QuizModeId, campaign.BookId!.Value);
    }

    private static async Task<int> ModeIdAsync(LeaderboardApiFactory factory, string slug)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await QuizModeSeed.GetIdAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(), slug);
    }

    private static async Task<AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }
}
