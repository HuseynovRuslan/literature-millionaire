using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

public class BilikYarisiSeedTests
{
    // --- the reviewed data file ----------------------------------------------

    [Fact]
    public void Bank_is_valid_and_has_the_approved_counts()
    {
        var questions = BilikYarisiSeed.LoadQuestions();

        Assert.Empty(BilikYarisiSeed.Validate(questions));
        Assert.Equal(441, questions.Count);
        Assert.Equal(
            new Dictionary<string, int> { ["Flags"] = 100, ["Capitals"] = 100, ["Azerbaijan"] = 83, ["GeneralKnowledge"] = 119, ["AzerbaijaniCinema"] = 39 },
            questions.GroupBy(q => q.Category).ToDictionary(g => g.Key, g => g.Count()));
        Assert.Equal(176, questions.Count(q => q.Difficulty == Difficulty.Easy));
        Assert.Equal(151, questions.Count(q => q.Difficulty == Difficulty.Medium));
        Assert.Equal(114, questions.Count(q => q.Difficulty == Difficulty.Hard));
        Assert.Equal(
            Enumerable.Range(1, 100).Select(i => $"/question-images/flag-{i:000}.webp"),
            questions.Where(q => q.ImageUrl != null).Select(q => q.ImageUrl!).Order(StringComparer.Ordinal));
        Assert.All(questions, q => Assert.DoesNotContain("flag-images/", q.ImageUrl ?? string.Empty));
    }

    [Fact]
    public void Validation_reports_broken_rows()
    {
        var questions = BilikYarisiSeed.LoadQuestions().ToList();
        var flag = questions.First(q => q.ImageUrl != null);
        var correctAnswer = flag.Options["ABCD".IndexOf(flag.CorrectOption[0])];
        questions[0] = flag with { ImageUrl = "flag-images/flag-001.webp", ImageAltText = $"Bayraq: {correctAnswer}", CorrectOption = "E" };
        questions[1] = questions[1] with { OptionB = questions[1].OptionA };
        questions.Add(questions[5]);

        var problems = string.Join("\n", BilikYarisiSeed.Validate(questions));

        Assert.Contains("Expected 441 questions", problems);
        Assert.Contains("needs ImageUrl /question-images/flag-NNN.webp", problems);
        Assert.Contains("is not A-D", problems);
        Assert.Contains("options are not four distinct values", problems);
        Assert.Contains("Duplicate SourceId", problems);
    }

    [Fact]
    public void Validation_rejects_alt_text_that_names_the_answer()
    {
        var questions = BilikYarisiSeed.LoadQuestions().ToList();
        var index = questions.FindIndex(q => q.ImageUrl != null);
        var flag = questions[index];
        questions[index] = flag with { ImageAltText = "Bu " + flag.Options["ABCD".IndexOf(flag.CorrectOption[0])] + " bayrağıdır" };

        Assert.Contains(BilikYarisiSeed.Validate(questions), p => p.Contains("alt text reveals the correct answer"));
    }

    [Fact]
    public void Every_flag_image_is_a_real_webp_in_frontend_public()
    {
        var folder = Path.Combine(RepositoryRoot(), "frontend", "public", "question-images");
        foreach (var url in BilikYarisiSeed.LoadQuestions().Where(q => q.ImageUrl != null).Select(q => q.ImageUrl!))
        {
            var path = Path.Combine(folder, Path.GetFileName(url));
            Assert.True(File.Exists(path), $"missing {path}");
            var bytes = File.ReadAllBytes(path);
            Assert.True(bytes.Length > 1000, $"{url} is too small");
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            Assert.Equal("WEBP", System.Text.Encoding.ASCII.GetString(bytes, 8, 4));
            Assert.Equal(bytes.Length, BitConverter.ToInt32(bytes, 4) + 8); // RIFF size matches: not truncated
        }
    }

    // --- seeding -------------------------------------------------------------

    [Fact]
    public async Task Seeding_is_idempotent_keeps_Oluler_and_switches_the_active_campaign_once()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();

        await SeedAsync(factory);
        var first = await SnapshotAsync(factory);
        await SeedAsync(factory);
        await SeedAsync(factory);
        var third = await SnapshotAsync(factory);

        Assert.Equal(first, third);
        Assert.Equal(30, first.OlulerQuestions);
        Assert.Equal(441, first.BilikQuestions);
        Assert.Equal(100, first.BilikImages);
        Assert.Equal(100, first.BilikDistinctImages);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campaigns = await db.MonthlyCampaigns.Include(c => c.Book).OrderBy(c => c.Id).ToListAsync();
        var oluler = Assert.Single(campaigns, c => c.Book.Title == OlulerQuestionSeed.BookTitle);
        var bilik = Assert.Single(campaigns, c => c.Book.Title == BilikYarisiSeed.BookTitle);
        Assert.False(oluler.IsEnabled);
        Assert.True(bilik.IsEnabled);
        Assert.Single(campaigns, c => c.IsEnabled);
        Assert.Equal((new DateOnly(2026, 9, 15), new DateOnly(2026, 9, 30), 8), (bilik.StartDate, bilik.EndDate, bilik.PassingScore));
        Assert.Equal(oluler.RewardTitle, bilik.RewardTitle);
        Assert.Equal((BilikYarisiSeed.BookAuthor, string.Empty, true), (bilik.Book.Author, bilik.Book.CoverImageUrl, bilik.Book.IsActive));
    }

    [Fact]
    public async Task Reseeding_never_overwrites_admin_changes_or_flips_campaigns_back()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();
        await SeedAsync(factory);

        int editedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var question = await db.Questions.Include(q => q.Book).FirstAsync(q => q.Book!.Title == BilikYarisiSeed.BookTitle && q.ImageUrl != null);
            question.OptionD = "Admin tərəfindən dəyişdirilib";
            question.Explanation = "Admin izahı";
            editedId = question.Id;
            foreach (var campaign in await db.MonthlyCampaigns.ToListAsync())
            {
                campaign.IsEnabled = !campaign.IsEnabled; // admin swaps back to "Ölülər"
            }
            await db.SaveChangesAsync();
        }
        var before = await SnapshotAsync(factory);

        await SeedAsync(factory);

        Assert.Equal(before, await SnapshotAsync(factory));
        await using var verify = factory.Services.CreateAsyncScope();
        var check = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var edited = await check.Questions.SingleAsync(q => q.Id == editedId);
        Assert.Equal(("Admin tərəfindən dəyişdirilib", "Admin izahı"), (edited.OptionD, edited.Explanation));
        var enabled = await check.MonthlyCampaigns.Include(c => c.Book).SingleAsync(c => c.IsEnabled);
        Assert.Equal(OlulerQuestionSeed.BookTitle, enabled.Book.Title);
    }

    // --- sessions ------------------------------------------------------------

    [Fact]
    public async Task Sessions_get_ten_unique_questions_3_4_3_with_exactly_two_flags_and_no_answer_leak()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();
        await SeedAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The seeded campaign is dated September 2026; move it onto "today" so the test is not date-bound.
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var campaign = await db.MonthlyCampaigns.SingleAsync(c => c.IsEnabled);
            campaign.StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
            campaign.EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1));
            await db.SaveChangesAsync();
        }

        for (var session = 0; session < 12; session++)
        {
            using var start = await client.PostAsJsonAsync("/api/game/start", new { fullName = "Test İştirakçı", phoneNumber = $"+9945010000{session:00}" });
            Assert.Equal(HttpStatusCode.OK, start.StatusCode);
            var startRaw = await start.Content.ReadAsStringAsync();
            AssertNoAnswerLeak(startRaw);
            var body = JsonDocument.Parse(startRaw).RootElement;
            var sessionId = body.GetProperty("sessionId").GetGuid();
            var question = body.GetProperty("question");

            var seen = new List<(int Id, string Difficulty, string? Image)>();
            for (var number = 1; number <= 10; number++)
            {
                seen.Add((question.GetProperty("id").GetInt32(), question.GetProperty("difficulty").GetString()!,
                    question.GetProperty("imageUrl").ValueKind == JsonValueKind.Null ? null : question.GetProperty("imageUrl").GetString()));

                using var answer = await client.PostAsJsonAsync($"/api/game/{sessionId}/answer", new { questionId = question.GetProperty("id").GetInt32(), selectedOption = "A" });
                Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
                var raw = await answer.Content.ReadAsStringAsync();
                var result = JsonDocument.Parse(raw).RootElement;
                if (number < 10)
                {
                    AssertNoAnswerLeak(raw);
                    Assert.False(result.GetProperty("isGameOver").GetBoolean());
                    question = result.GetProperty("nextQuestion");
                }
                else
                {
                    Assert.True(result.GetProperty("isGameOver").GetBoolean());
                    Assert.Equal(20, result.GetProperty("result").GetProperty("maxPoints").GetInt32());
                }
            }

            Assert.Equal(10, seen.Select(q => q.Id).Distinct().Count());
            Assert.Equal((3, 4, 3), (seen.Count(q => q.Difficulty == "Easy"), seen.Count(q => q.Difficulty == "Medium"), seen.Count(q => q.Difficulty == "Hard")));
            Assert.Equal(2, seen.Count(q => q.Image != null));
            Assert.All(seen.Where(q => q.Image != null), q => Assert.Matches(@"^/question-images/flag-\d{3}\.webp$", q.Image!));
        }
    }

    private static void AssertNoAnswerLeak(string raw)
    {
        foreach (var forbidden in new[] { "correctOption", "explanation", "isCorrect", "pointsEarned", "correctAnswers" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    // --- helpers -------------------------------------------------------------

    private sealed record Snapshot(int Books, int Campaigns, int EnabledCampaigns, int OlulerQuestions, string OlulerFingerprint, int BilikQuestions, int BilikImages, int BilikDistinctImages, string BilikFingerprint);

    private static async Task SeedAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
    }

    private static async Task<Snapshot> SnapshotAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var books = await db.Books.ToListAsync();
        var oluler = books.Single(b => b.Title == OlulerQuestionSeed.BookTitle).Id;
        var bilik = books.Single(b => b.Title == BilikYarisiSeed.BookTitle).Id;
        var questions = await db.Questions.AsNoTracking().OrderBy(q => q.Id).ToListAsync();
        string Fingerprint(int bookId) => string.Join("|", questions.Where(q => q.BookId == bookId)
            .Select(q => $"{q.Id}:{q.Text}:{q.OptionA}:{q.OptionB}:{q.OptionC}:{q.OptionD}:{q.CorrectOption}:{q.Difficulty}:{q.Explanation}:{q.ImageUrl}"));
        var bilikQuestions = questions.Where(q => q.BookId == bilik).ToList();
        return new Snapshot(
            books.Count,
            await db.MonthlyCampaigns.CountAsync(),
            await db.MonthlyCampaigns.CountAsync(c => c.IsEnabled),
            questions.Count(q => q.BookId == oluler),
            Fingerprint(oluler),
            bilikQuestions.Count,
            bilikQuestions.Count(q => q.ImageUrl != null),
            bilikQuestions.Where(q => q.ImageUrl != null).Select(q => q.ImageUrl).Distinct().Count(),
            Fingerprint(bilik));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "public", "question-images")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root with frontend/public/question-images was not found.");
    }
}
