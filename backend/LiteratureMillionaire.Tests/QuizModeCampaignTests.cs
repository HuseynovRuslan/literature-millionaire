using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>Quiz modes: campaigns per mode, the available list, starting a chosen campaign and per-mode question selection.</summary>
public class QuizModeCampaignTests
{
    private const string FullName = "Ayşə Məmmədova";

    // --- seed and bank assignment ---------------------------------------------------

    [Fact]
    public async Task Seed_creates_the_four_modes_and_assigns_banks_and_campaigns_idempotently()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();

        for (var run = 0; run < 2; run++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var modes = await db.QuizModes.AsNoTracking().OrderBy(m => m.DisplayOrder).ToListAsync();
        Assert.Equal(
            new[] { ("bilik-dunyasi", "Bilik Dünyası", 1), ("ayin-kitabi", "Ayın Kitabı", 2), ("edebiyyat-dunyasi", "Ədəbiyyat Dünyası", 3), ("yasil-baki", "Yaşıl Bakı", 4) },
            modes.Select(m => (m.Slug, m.Title, m.DisplayOrder)));
        Assert.All(modes, m => Assert.True(m.IsActive));

        var bilikMode = modes[0].Id;
        var ayinMode = modes[1].Id;
        var questions = await db.Questions.AsNoTracking().Include(q => q.Book).ToListAsync();
        var bilik = questions.Where(q => q.Book?.Title == BilikYarisiSeed.BookTitle).ToList();
        var oluler = questions.Where(q => q.Book?.Title == OlulerQuestionSeed.BookTitle).ToList();
        Assert.Equal(441, bilik.Count);
        Assert.All(bilik, q => Assert.Equal(bilikMode, q.QuizModeId));
        Assert.Equal(30, oluler.Count);
        Assert.All(oluler, q => Assert.Equal(ayinMode, q.QuizModeId));
        Assert.All(questions.Where(q => q.BookId == null), q => Assert.Null(q.QuizModeId));

        var campaigns = await db.MonthlyCampaigns.AsNoTracking().Include(c => c.Book).ToListAsync();
        Assert.Equal(3, campaigns.Count);
        Assert.Equal(bilikMode, campaigns.Single(c => c.Book!.Title == BilikYarisiSeed.BookTitle).QuizModeId);
        Assert.Equal(ayinMode, campaigns.Single(c => c.Book!.Title == OlulerQuestionSeed.BookTitle).QuizModeId);
        Assert.Equal(modes[3].Id, campaigns.Single(c => c.Book!.Title == YasilBakiSeed.BookTitle).QuizModeId);
        // The mixed banks ask for two pictures in a round; every Yaşıl Bakı question is a photograph, so
        // its round is ten of them.
        Assert.All(campaigns.Where(c => c.Book!.Title != YasilBakiSeed.BookTitle),
            c => Assert.Equal(2, c.ImageQuestionsPerQuiz));
        Assert.Equal(10, campaigns.Single(c => c.Book!.Title == YasilBakiSeed.BookTitle).ImageQuestionsPerQuiz);
    }

    [Theory]
    [InlineData("bilik-dunyasi", true)]
    [InlineData("yasil-baki", true)]
    [InlineData("mode2", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("Bilik-Dunyasi", false)]
    [InlineData("bilik dunyasi", false)]
    [InlineData("-bilik", false)]
    [InlineData("bilik-", false)]
    [InlineData("bilik--dunyasi", false)]
    [InlineData("bilik_dunyasi", false)]
    [InlineData("yaşıl-bakı", false)]
    [InlineData("bilik\n", false)]
    public void Slug_format_is_lowercase_ascii_words_joined_by_single_hyphens(string? slug, bool valid) =>
        Assert.Equal(valid, QuizMode.IsValidSlug(slug));

    [Fact]
    public void Slug_length_is_limited()
    {
        Assert.True(QuizMode.IsValidSlug(new string('a', QuizMode.SlugMaxLength)));
        Assert.False(QuizMode.IsValidSlug(new string('a', QuizMode.SlugMaxLength + 1)));
        Assert.All(QuizModeSeed.Modes, m => Assert.True(QuizMode.IsValidSlug(m.Slug)));
    }

    [Fact]
    public async Task Database_rejects_duplicate_or_malformed_slugs_and_out_of_range_image_targets()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: false);

        await AssertRejectedAsync(factory, db => db.QuizModes.Add(Mode(QuizModeSlugs.BilikDunyasi)));
        await AssertRejectedAsync(factory, db => db.QuizModes.Add(Mode("Bad Slug")));
        await AssertRejectedAsync(factory, db => db.QuizModes.Add(Mode("trailing-")));

        foreach (var (target, accepted) in new[] { (-1, false), (11, false), (0, true), (10, true) })
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.MonthlyCampaigns.Add(new MonthlyCampaign
            {
                QuizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.YasilBaki),
                StartDate = new DateOnly(2020, 1, 1),
                EndDate = new DateOnly(2020, 1, 2),
                PassingScore = 7,
                RewardTitle = "Test",
                ImageQuestionsPerQuiz = target
            });
            if (accepted)
            {
                await db.SaveChangesAsync();
            }
            else
            {
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }
        }
    }

    // --- available / current ---------------------------------------------------------

    [Fact]
    public async Task Available_lists_one_playable_campaign_per_active_mode_in_display_order()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var yasil = await AddCampaignAsync(factory, QuizModeSlugs.YasilBaki, "yasil", imageTarget: 8, imagesPerDifficulty: 4, withBook: false);
        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var ayin = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin", imageTarget: 0);
        // Not listed: a disabled or finished campaign, a future one, and a campaign whose mode is inactive.
        await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik-disabled", enabled: false);
        await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin-past", startOffset: -30, endOffset: -1);
        await AddCampaignAsync(factory, QuizModeSlugs.YasilBaki, "yasil-future", startOffset: 1, endOffset: 30);
        await AddCampaignAsync(factory, QuizModeSlugs.EdebiyyatDunyasi, "edebiyyat");
        await SetModeActiveAsync(factory, QuizModeSlugs.EdebiyyatDunyasi, false);

        using var response = await client.GetAsync("/api/campaigns/available");
        var raw = await response.Content.ReadAsStringAsync();
        var list = JsonDocument.Parse(raw).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { bilik, ayin, yasil }, list.EnumerateArray().Select(c => c.GetProperty("campaignId").GetInt32()));
        Assert.Equal(new[] { "bilik-dunyasi", "ayin-kitabi", "yasil-baki" },
            list.EnumerateArray().Select(c => c.GetProperty("quizMode").GetProperty("slug").GetString()));

        var first = list[0];
        Assert.Equal(
            new[] { "campaignId", "startDate", "endDate", "passingScore", "rewardTitle", "questionCount", "imageQuestionsPerQuiz", "quizMode", "book" },
            first.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            new[] { "id", "slug", "title", "description", "iconKey", "displayOrder" },
            first.GetProperty("quizMode").EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Bilik Dünyası", "globe", 1), (first.GetProperty("quizMode").GetProperty("title").GetString(),
            first.GetProperty("quizMode").GetProperty("iconKey").GetString(), first.GetProperty("quizMode").GetProperty("displayOrder").GetInt32()));
        Assert.Equal(10, first.GetProperty("questionCount").GetInt32());
        Assert.Equal(2, first.GetProperty("imageQuestionsPerQuiz").GetInt32());
        Assert.Equal(0, list[1].GetProperty("imageQuestionsPerQuiz").GetInt32());
        Assert.Equal("ayin kitabı", list[1].GetProperty("book").GetProperty("title").GetString());
        Assert.Equal(8, list[2].GetProperty("imageQuestionsPerQuiz").GetInt32());
        Assert.Equal(JsonValueKind.Null, list[2].GetProperty("book").ValueKind);
        AssertNoPrivateFields(raw);
    }

    [Fact]
    public async Task Available_and_current_return_404_with_a_stable_code_when_nothing_is_playable()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik", enabled: false);

        await AssertProblemAsync(await client.GetAsync("/api/campaigns/available"), HttpStatusCode.NotFound, "NO_ACTIVE_CAMPAIGN");
        await AssertProblemAsync(await client.GetAsync("/api/campaigns/current"), HttpStatusCode.NotFound, "NO_ACTIVE_CAMPAIGN");
    }

    [Fact]
    public async Task Current_is_the_bilik_dunyasi_campaign_only()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");

        // Another mode is playable, but the default one is not.
        await AssertProblemAsync(await client.GetAsync("/api/campaigns/current"), HttpStatusCode.NotFound, "NO_ACTIVE_CAMPAIGN");
        using (var available = await client.GetAsync("/api/campaigns/available"))
        {
            Assert.Equal(HttpStatusCode.OK, available.StatusCode);
        }

        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        using var response = await client.GetAsync("/api/campaigns/current");
        var current = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(bilik, current.GetProperty("campaignId").GetInt32());
        // The pre-mode contract is unchanged.
        Assert.Equal(
            new[] { "campaignId", "startDate", "endDate", "passingScore", "rewardTitle", "questionCount", "book" },
            current.EnumerateObject().Select(p => p.Name));
        Assert.Equal(
            new[] { "id", "title", "author", "description", "coverImageUrl" },
            current.GetProperty("book").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Two_campaigns_of_the_same_mode_on_one_date_fail_safely_and_are_logged_without_personal_data()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var first = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik-1");
        await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik-2", startOffset: 0, endOffset: 5);
        await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");

        await AssertProblemAsync(await client.GetAsync("/api/campaigns/available"), HttpStatusCode.InternalServerError, "MULTIPLE_ACTIVE_CAMPAIGNS");
        await AssertProblemAsync(await client.GetAsync("/api/campaigns/current"), HttpStatusCode.InternalServerError, "MULTIPLE_ACTIVE_CAMPAIGNS");
        await AssertProblemAsync(await StartAsync(client, "+994501112233", first), HttpStatusCode.InternalServerError, "MULTIPLE_ACTIVE_CAMPAIGNS");
        await AssertProblemAsync(await StartAsync(client, "+994501112233", null), HttpStatusCode.InternalServerError, "MULTIPLE_ACTIVE_CAMPAIGNS");

        Assert.Contains(factory.Logs, line => line.Contains("MULTIPLE_ACTIVE_CAMPAIGNS", StringComparison.Ordinal) || line.Contains("quiz mode bilik-dunyasi has 2", StringComparison.Ordinal));
        AssertLogsHaveNoPersonalData(factory, "501112233");
        Assert.Equal(0, await CountAsync(factory, db => db.QuizAttempts.CountAsync()));
    }

    [Fact]
    public async Task Campaigns_of_different_modes_may_run_on_the_same_dates()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var ayin = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");
        var edebiyyat = await AddCampaignAsync(factory, QuizModeSlugs.EdebiyyatDunyasi, "edebiyyat", withBook: false);

        var available = await client.GetFromJsonAsync<JsonElement>("/api/campaigns/available");
        var current = await client.GetFromJsonAsync<JsonElement>("/api/campaigns/current");

        Assert.Equal(new[] { bilik, ayin, edebiyyat }, available.EnumerateArray().Select(c => c.GetProperty("campaignId").GetInt32()));
        Assert.Equal(bilik, current.GetProperty("campaignId").GetInt32());
    }

    // --- starting a campaign ---------------------------------------------------------

    [Fact]
    public async Task Start_with_a_campaign_id_plays_only_that_modes_questions_and_without_one_plays_bilik_dunyasi()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var ayin = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");
        int ayinBook;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            ayinBook = (await db.MonthlyCampaigns.SingleAsync(c => c.Id == ayin)).BookId!.Value;
            var ayinMode = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.AyinKitabi);
            var bilikMode = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.BilikDunyasi);
            var otherBook = new Book { Title = "Başqa kitab", Author = "Müəllif", Description = "", CoverImageUrl = "" };
            db.Books.Add(otherBook);
            await db.SaveChangesAsync();
            // Decoys: same mode but another book, the campaign's book without a mode (legacy), the book in another mode.
            AddQuestions(db, "same-mode-other-book", ayinMode, otherBook.Id, images: 0);
            AddQuestions(db, "legacy", null, ayinBook, images: 0);
            AddQuestions(db, "other-mode", bilikMode, ayinBook, images: 0);
            AddQuestions(db, "legacy-no-book", null, null, images: 0);
            await db.SaveChangesAsync();
            await NameOptionsAsync(db);
        }

        for (var i = 0; i < 5; i++)
        {
            var played = await PlayAsync(client, $"+99450200000{i}", ayin);
            Assert.Equal(ayin, played.Start.GetProperty("campaignId").GetInt32());
            Assert.Equal(("ayin-kitabi", "Ayın Kitabı"), (played.Start.GetProperty("quizMode").GetProperty("slug").GetString(), played.Start.GetProperty("quizMode").GetProperty("title").GetString()));
            Assert.Equal(new[] { "id", "slug", "title" }, played.Start.GetProperty("quizMode").EnumerateObject().Select(p => p.Name));
            Assert.All(played.Questions, q => Assert.Equal("ayin", q.Category));
            Assert.Equal(ayin, played.Result.GetProperty("campaignId").GetInt32());
            Assert.Equal("ayin-kitabi", played.Result.GetProperty("quizMode").GetProperty("slug").GetString());
            AssertNoPrivateFields(played.StartRaw);
            AssertNoPrivateFields(played.FinalRaw);
        }

        var defaultGame = await PlayAsync(client, "+994502000009", null);
        Assert.Equal(bilik, defaultGame.Start.GetProperty("campaignId").GetInt32());
        Assert.Equal("bilik-dunyasi", defaultGame.Start.GetProperty("quizMode").GetProperty("slug").GetString());
        Assert.All(defaultGame.Questions, q => Assert.Equal("bilik", q.Category));
    }

    [Fact]
    public async Task Start_rejects_unknown_or_unplayable_campaigns_without_using_an_attempt()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var disabled = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin-disabled", enabled: false);
        var past = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin-past", startOffset: -20, endOffset: -2);
        var future = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin-future", startOffset: 2, endOffset: 20);
        var inactiveMode = await AddCampaignAsync(factory, QuizModeSlugs.EdebiyyatDunyasi, "edebiyyat", withBook: false);
        await SetModeActiveAsync(factory, QuizModeSlugs.EdebiyyatDunyasi, false);
        var bookless = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin-bookless", withBook: false);

        const string phone = "+994503000000";
        await AssertProblemAsync(await StartAsync(client, phone, 999999), HttpStatusCode.NotFound, "CAMPAIGN_NOT_FOUND");
        await AssertProblemAsync(await StartAsync(client, phone, disabled), HttpStatusCode.Conflict, "CAMPAIGN_NOT_ACTIVE");
        await AssertProblemAsync(await StartAsync(client, phone, past), HttpStatusCode.Conflict, "CAMPAIGN_NOT_ACTIVE");
        await AssertProblemAsync(await StartAsync(client, phone, future), HttpStatusCode.Conflict, "CAMPAIGN_NOT_ACTIVE");
        await AssertProblemAsync(await StartAsync(client, phone, inactiveMode), HttpStatusCode.Conflict, "CAMPAIGN_NOT_ACTIVE");
        await AssertProblemAsync(await StartAsync(client, phone, bookless), HttpStatusCode.InternalServerError, "CAMPAIGN_BOOK_REQUIRED");
        using (var zero = await StartAsync(client, phone, 0))
        {
            Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        }

        // The misconfigured "Ayın Kitabı" campaign is not offered either.
        var available = await client.GetFromJsonAsync<JsonElement>("/api/campaigns/available");
        Assert.Equal(new[] { "bilik-dunyasi" }, available.EnumerateArray().Select(c => c.GetProperty("quizMode").GetProperty("slug").GetString()));

        Assert.Equal(0, await CountAsync(factory, db => db.QuizAttempts.CountAsync()));
        Assert.Equal(0, await CountAsync(factory, db => db.Participants.CountAsync()));
        AssertLogsHaveNoPersonalData(factory, "503000000");
    }

    [Fact]
    public async Task One_attempt_per_campaign_the_same_phone_may_play_another_mode()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var ayin = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");
        const string phone = "+994504000000";

        using (var first = await StartAsync(client, phone, null))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }
        await AssertProblemAsync(await StartAsync(client, "050 400 00 00", bilik), HttpStatusCode.Conflict, "ATTEMPT_LIMIT_REACHED");

        using (var other = await StartAsync(client, phone, ayin))
        {
            Assert.Equal(HttpStatusCode.OK, other.StatusCode);
            var body = await other.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(1, body.GetProperty("attemptNumber").GetInt32());
        }
        await AssertProblemAsync(await StartAsync(client, phone, ayin), HttpStatusCode.Conflict, "ATTEMPT_LIMIT_REACHED");

        Assert.Equal(1, await CountAsync(factory, db => db.Participants.CountAsync()));
        Assert.Equal(1, await CountAsync(factory, db => db.QuizAttempts.CountAsync(a => a.CampaignId == bilik)));
        Assert.Equal(1, await CountAsync(factory, db => db.QuizAttempts.CountAsync(a => a.CampaignId == ayin)));
    }

    [Fact]
    public async Task Each_campaign_keeps_its_own_leaderboard()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var bilik = await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var ayin = await AddCampaignAsync(factory, QuizModeSlugs.AyinKitabi, "ayin");

        var bilikGame = await PlayAsync(client, "+994505000001", bilik);
        var ayinGame = await PlayAsync(client, "+994505000002", ayin);
        var bothGame = await PlayAsync(client, "+994505000001", ayin);

        Assert.Equal(1, bilikGame.Result.GetProperty("leaderboardPosition").GetInt32());
        Assert.Equal(1, ayinGame.Result.GetProperty("leaderboardPosition").GetInt32());
        Assert.InRange(bothGame.Result.GetProperty("leaderboardPosition").GetInt32(), 1, 2);

        using var bilikBoard = await client.GetAsync($"/api/campaigns/{bilik}/leaderboard");
        using var ayinBoard = await client.GetAsync($"/api/campaigns/{ayin}/leaderboard");
        var bilikRaw = await bilikBoard.Content.ReadAsStringAsync();
        var ayinRaw = await ayinBoard.Content.ReadAsStringAsync();
        Assert.Single(JsonDocument.Parse(bilikRaw).RootElement.GetProperty("entries").EnumerateArray());
        Assert.Equal(2, JsonDocument.Parse(ayinRaw).RootElement.GetProperty("entries").GetArrayLength());
        foreach (var raw in new[] { bilikRaw, ayinRaw })
        {
            Assert.DoesNotContain("Məmmədova", raw);
            AssertNoPrivateFields(raw);
        }

        AssertLogsHaveNoPersonalData(factory, "505000001", "505000002");
    }

    // --- question mix per campaign ---------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    public async Task Sessions_use_the_campaigns_image_target_with_the_fixed_mix_and_no_leak(int imageTarget)
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await AddCampaignAsync(factory, QuizModeSlugs.BilikDunyasi, "bilik");
        var campaign = await AddCampaignAsync(factory, QuizModeSlugs.YasilBaki, "yasil", imageTarget: imageTarget, imagesPerDifficulty: 4, withBook: false);

        var shuffled = false;
        for (var session = 0; session < 6; session++)
        {
            var played = await PlayAsync(client, $"+99450600000{session}", campaign);

            Assert.Equal(10, played.Start.GetProperty("secondsPerQuestion").GetInt32());
            Assert.Equal(10, played.Start.GetProperty("totalQuestions").GetInt32());
            var remaining = played.Start.GetProperty("questionExpiresAtUtc").GetDateTime().ToUniversalTime() - DateTime.UtcNow;
            Assert.InRange(remaining.TotalSeconds, 0, 10.5);

            Assert.Equal(10, played.Questions.Select(q => q.Id).Distinct().Count());
            Assert.Equal((3, 4, 3), (played.Questions.Count(q => q.Difficulty == "Easy"), played.Questions.Count(q => q.Difficulty == "Medium"), played.Questions.Count(q => q.Difficulty == "Hard")));
            Assert.Equal(imageTarget, played.Questions.Count(q => q.HasImage));
            Assert.All(played.Questions, q => Assert.Equal("yasil", q.Category));
            shuffled |= played.Questions.Any(q => q.OptionA != $"q{q.Id}-A");

            AssertNoAnswerLeak(played.StartRaw);
            Assert.All(played.AnswerRaws.Take(9), AssertNoAnswerLeak);
            Assert.Equal(20, played.Result.GetProperty("maxPoints").GetInt32());
        }

        Assert.True(shuffled, "Options were never shuffled across 60 delivered questions.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(10)]
    public void Planner_meets_any_image_target_the_pool_allows(int imageTarget)
    {
        var pool = PlannerPool(imagesPerDifficulty: 4, textsPerDifficulty: 4);
        for (var seed = 0; seed < 40; seed++)
        {
            var plan = QuestionMixPlanner.Plan(pool, imageTarget, new Random(seed));
            Assert.Equal(10, plan.Select(q => q.Id).Distinct().Count());
            Assert.Equal((3, 4, 3), (plan.Count(q => q.Difficulty == Difficulty.Easy), plan.Count(q => q.Difficulty == Difficulty.Medium), plan.Count(q => q.Difficulty == Difficulty.Hard)));
            Assert.Equal(imageTarget, plan.Count(q => q.HasImage));
        }
    }

    [Fact]
    public void Planner_uses_fewer_images_when_the_pool_has_fewer_and_rejects_targets_outside_0_to_10()
    {
        var sparse = PlannerPool(imagesPerDifficulty: 1, textsPerDifficulty: 4);
        Assert.Equal(3, QuestionMixPlanner.Plan(sparse, 8, new Random(1)).Count(q => q.HasImage));

        var noText = PlannerPool(imagesPerDifficulty: 4, textsPerDifficulty: 0);
        Assert.Equal(10, QuestionMixPlanner.Plan(noText, 0, new Random(2)).Count(q => q.HasImage));

        Assert.Throws<ArgumentOutOfRangeException>(() => QuestionMixPlanner.Plan(sparse, -1, new Random(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => QuestionMixPlanner.Plan(sparse, 11, new Random(4)));
    }

    // --- helpers ---------------------------------------------------------------------

    private sealed record DeliveredQuestion(int Id, string Category, string Difficulty, bool HasImage, string OptionA);

    private sealed record PlayedGame(JsonElement Start, string StartRaw, IReadOnlyList<DeliveredQuestion> Questions, IReadOnlyList<string> AnswerRaws, JsonElement Result, string FinalRaw);

    /// <summary>
    /// Adds a playable (by default) campaign of the mode with its own question bank: per difficulty quota+1 text
    /// questions and <paramref name="imagesPerDifficulty"/> illustrated ones, all in <paramref name="category"/>.
    /// </summary>
    private static async Task<int> AddCampaignAsync(
        LeaderboardApiFactory factory,
        string modeSlug,
        string category,
        int imageTarget = QuizRules.DefaultImageQuestionsPerQuiz,
        int imagesPerDifficulty = 1,
        bool withBook = true,
        int startOffset = -1,
        int endOffset = 1,
        bool enabled = true)
    {
        await factory.CreateDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await QuizModeSeed.SeedAsync(db);
        var modeId = await QuizModeSeed.GetIdAsync(db, modeSlug);

        Book? book = withBook ? new Book { Title = $"{category} kitabı", Author = "Müəllif", Description = "Təsvir", CoverImageUrl = "" } : null;
        var campaign = new MonthlyCampaign
        {
            QuizModeId = modeId,
            Book = book,
            StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(startOffset)),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(endOffset)),
            PassingScore = 7,
            RewardTitle = "Hədiyyə",
            ImageQuestionsPerQuiz = imageTarget
        };
        db.MonthlyCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        AddQuestions(db, category, modeId, book?.Id, imagesPerDifficulty);
        await db.SaveChangesAsync();
        await NameOptionsAsync(db);

        if (!enabled)
        {
            // IsEnabled has a database default of true, so false is applied after the insert.
            await db.MonthlyCampaigns.Where(c => c.Id == campaign.Id).ExecuteUpdateAsync(set => set.SetProperty(c => c.IsEnabled, false));
        }

        return campaign.Id;
    }

    private static void AddQuestions(ApplicationDbContext db, string category, int? modeId, int? bookId, int images)
    {
        foreach (var difficulty in new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard })
        {
            var texts = QuizRules.QuotaFor(difficulty) + 1;
            for (var i = 0; i < texts + images; i++)
            {
                var illustrated = i >= texts;
                db.Questions.Add(new Question
                {
                    QuizModeId = modeId,
                    BookId = bookId,
                    Text = $"{category} {difficulty} {i}",
                    OptionA = "pending-A",
                    OptionB = "pending-B",
                    OptionC = "pending-C",
                    OptionD = "pending-D",
                    CorrectOption = 'A',
                    Difficulty = difficulty,
                    Category = category,
                    ImageUrl = illustrated ? $"/question-images/{category}-{difficulty}-{i}.webp" : null,
                    ImageAltText = illustrated ? "Şəkil" : null
                });
            }
        }
    }

    /// <summary>Option texts carry the question id ("q12-A") so the tests can see whether options were shuffled.</summary>
    private static Task NameOptionsAsync(ApplicationDbContext db) =>
        db.Questions
            .Where(q => q.OptionA == "pending-A")
            .ExecuteUpdateAsync(set => set
                .SetProperty(q => q.OptionA, q => "q" + q.Id + "-A")
                .SetProperty(q => q.OptionB, q => "q" + q.Id + "-B")
                .SetProperty(q => q.OptionC, q => "q" + q.Id + "-C")
                .SetProperty(q => q.OptionD, q => "q" + q.Id + "-D"));

    private static async Task SetModeActiveAsync(LeaderboardApiFactory factory, string slug, bool active)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.QuizModes.Where(m => m.Slug == slug).ExecuteUpdateAsync(set => set.SetProperty(m => m.IsActive, active));
    }

    private static async Task<int> CountAsync(LeaderboardApiFactory factory, Func<ApplicationDbContext, Task<int>> count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await count(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string phone, int? campaignId) =>
        campaignId is null
            ? client.PostAsJsonAsync("/api/game/start", new { fullName = FullName, phoneNumber = phone })
            : client.PostAsJsonAsync("/api/game/start", new { fullName = FullName, phoneNumber = phone, campaignId });

    private static async Task<PlayedGame> PlayAsync(HttpClient client, string phone, int? campaignId)
    {
        using var start = await StartAsync(client, phone, campaignId);
        var startRaw = await start.Content.ReadAsStringAsync();
        Assert.True(start.StatusCode == HttpStatusCode.OK, startRaw);
        var startBody = JsonDocument.Parse(startRaw).RootElement;
        var sessionId = startBody.GetProperty("sessionId").GetGuid();
        var question = startBody.GetProperty("question");

        var questions = new List<DeliveredQuestion>();
        var answers = new List<string>();
        JsonElement final = default;
        for (var number = 1; number <= 10; number++)
        {
            var id = question.GetProperty("id").GetInt32();
            questions.Add(new DeliveredQuestion(
                id,
                question.GetProperty("category").GetString()!,
                question.GetProperty("difficulty").GetString()!,
                question.GetProperty("imageUrl").ValueKind != JsonValueKind.Null,
                question.GetProperty("optionA").GetString()!));

            using var answer = await client.PostAsJsonAsync($"/api/game/{sessionId}/answer", new { questionId = id, selectedOption = "A" });
            var raw = await answer.Content.ReadAsStringAsync();
            Assert.True(answer.StatusCode == HttpStatusCode.OK, raw);
            answers.Add(raw);
            var body = JsonDocument.Parse(raw).RootElement;
            if (number < 10)
            {
                Assert.False(body.GetProperty("isGameOver").GetBoolean());
                question = body.GetProperty("nextQuestion");
            }
            else
            {
                Assert.True(body.GetProperty("isGameOver").GetBoolean());
                final = body.GetProperty("result");
            }
        }

        return new PlayedGame(startBody, startRaw, questions, answers, final, answers[^1]);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync();
            Assert.True(response.StatusCode == status, $"Expected {status}, got {response.StatusCode}: {raw}");
            Assert.Equal(code, JsonDocument.Parse(raw).RootElement.GetProperty("code").GetString());
            AssertNoPrivateFields(raw);
        }
    }

    private static void AssertNoAnswerLeak(string raw)
    {
        foreach (var forbidden in new[] { "correctOption", "explanation", "isCorrect", "pointsEarned", "correctAnswers", "passed", "leaderboardPosition" })
        {
            Assert.DoesNotContain($"\"{forbidden}\"", raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertNoPrivateFields(string raw)
    {
        // JSON keys and personal values; generic wording such as "This phone number has used..." is allowed.
        foreach (var forbidden in new[] { "\"phone", "\"normalizedPhone", "\"participantId\"", "\"attemptId\"", "\"fullName\"", "Məmmədova", "+994" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void AssertLogsHaveNoPersonalData(LeaderboardApiFactory factory, params string[] phoneDigits)
    {
        var logs = string.Join("\n", factory.Logs);
        Assert.DoesNotContain("Məmmədova", logs);
        foreach (var digits in phoneDigits)
        {
            Assert.DoesNotContain(digits, logs);
        }
    }

    private static async Task AssertRejectedAsync(LeaderboardApiFactory factory, Action<ApplicationDbContext> change)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        change(db);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private static QuizMode Mode(string slug) => new()
    {
        Slug = slug,
        Title = "Test",
        Description = "Test",
        IconKey = "test",
        DisplayOrder = 9,
        IsActive = true
    };

    private static IReadOnlyList<PoolQuestion> PlannerPool(int imagesPerDifficulty, int textsPerDifficulty)
    {
        var id = 0;
        return new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard }
            .SelectMany(d => Enumerable.Range(0, imagesPerDifficulty + textsPerDifficulty)
                .Select(i => new PoolQuestion(++id, 'A', d, i < imagesPerDifficulty)))
            .ToArray();
    }
}
