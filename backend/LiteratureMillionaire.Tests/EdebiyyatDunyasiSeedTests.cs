using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>The reviewed "Ədəbiyyat Dünyası" bank: what the data file contains, how it imports, and how it plays.</summary>
public class EdebiyyatDunyasiSeedTests
{
    // --- the reviewed data file ----------------------------------------------

    [Fact]
    public void Bank_is_valid_and_has_the_approved_counts()
    {
        var questions = EdebiyyatDunyasiSeed.LoadQuestions();

        Assert.Empty(EdebiyyatDunyasiSeed.Validate(questions));
        Assert.Equal(150, questions.Count);
        Assert.Equal(45, questions.Count(q => q.Difficulty == Difficulty.Easy));
        Assert.Equal(60, questions.Count(q => q.Difficulty == Difficulty.Medium));
        Assert.Equal(45, questions.Count(q => q.Difficulty == Difficulty.Hard));
        // 30 illustrated questions, split 9 / 12 / 9 as the workbook requires.
        var illustrated = questions.Where(q => q.ImageUrl is not null).ToList();
        Assert.Equal(30, illustrated.Count);
        Assert.Equal(9, illustrated.Count(q => q.Difficulty == Difficulty.Easy));
        Assert.Equal(12, illustrated.Count(q => q.Difficulty == Difficulty.Medium));
        Assert.Equal(9, illustrated.Count(q => q.Difficulty == Difficulty.Hard));
        Assert.Equal(120, questions.Count(q => q.ImageUrl is null));
        // The reviewer's answer spread, kept as delivered.
        Assert.Equal(
            new Dictionary<string, int> { ["A"] = 38, ["B"] = 38, ["C"] = 37, ["D"] = 37 },
            questions.GroupBy(q => q.CorrectOption).ToDictionary(g => g.Key, g => g.Count()));
    }

    [Fact]
    public void Every_illustration_is_a_real_webp_in_frontend_public_and_carries_its_credit()
    {
        var illustrated = EdebiyyatDunyasiSeed.LoadQuestions().Where(q => q.ImageUrl is not null).ToList();
        var folder = Path.Combine(RepositoryRoot(), "frontend", "public", "question-images");

        Assert.All(illustrated, q => Assert.Matches(@"^/question-images/literature-\d{3}\.webp$", q.ImageUrl!));
        Assert.All(illustrated, q => Assert.True(
            File.Exists(Path.Combine(folder, Path.GetFileName(q.ImageUrl!))), $"missing file: {q.ImageUrl}"));
        Assert.Equal(30, Directory.GetFiles(folder, "literature-*.webp").Length);
        // Licences such as CC BY and CC BY-SA require the credit to stay with the picture.
        Assert.All(illustrated, q => Assert.False(string.IsNullOrWhiteSpace(q.ImageSource)));
        Assert.All(illustrated, q => Assert.False(string.IsNullOrWhiteSpace(q.ImageLicense)));
        Assert.All(illustrated, q => Assert.False(string.IsNullOrWhiteSpace(q.ImageAltText)));
        // Text-only questions carry no image metadata at all.
        Assert.All(EdebiyyatDunyasiSeed.LoadQuestions().Where(q => q.ImageUrl is null),
            q => Assert.True(q.ImageAltText is null && q.ImageSource is null && q.ImageLicense is null));
    }

    [Fact]
    public void No_alt_text_gives_its_answer_away()
    {
        foreach (var question in EdebiyyatDunyasiSeed.LoadQuestions().Where(q => q.ImageAltText is not null))
        {
            var answer = question.Options["ABCD".IndexOf(question.CorrectOption[0])];
            Assert.DoesNotContain(answer, question.ImageAltText!, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    [Fact]
    public void Validation_reports_broken_rows()
    {
        var questions = EdebiyyatDunyasiSeed.LoadQuestions().ToList();
        var illustrated = questions.First(q => q.ImageUrl is not null);
        var answer = illustrated.Options["ABCD".IndexOf(illustrated.CorrectOption[0])];
        questions[questions.IndexOf(illustrated)] = illustrated with { ImageAltText = $"Şəkildə {answer} göstərilib", ImageLicense = null };
        questions[1] = questions[1] with { CorrectOption = "E" };
        questions[2] = questions[2] with { OptionB = questions[2].OptionA };
        questions.Add(questions[5]);

        var problems = string.Join("\n", EdebiyyatDunyasiSeed.Validate(questions));

        Assert.Contains("Expected 150 questions", problems);
        Assert.Contains("is not A-D", problems);
        Assert.Contains("options are not four distinct values", problems);
        Assert.Contains("Duplicate SourceId", problems);
        Assert.Contains("alt text names the answer", problems);
        Assert.Contains("has no image licence", problems);
    }

    // --- import --------------------------------------------------------------

    [Fact]
    public async Task Seeding_imports_the_bank_once_and_is_idempotent()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();

        for (var run = 0; run < 3; run++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var book = await db.Books.SingleAsync(b => b.Title == EdebiyyatDunyasiSeed.BookTitle);
        var questions = await db.Questions.Where(q => q.BookId == book.Id).ToListAsync();

        Assert.Equal(150, questions.Count);
        Assert.Equal(30, questions.Count(q => q.ImageUrl != null));
        Assert.Equal(30, questions.Count(q => q.ImageSource != null && q.ImageLicense != null));
        Assert.Equal((45, 60, 45), (
            questions.Count(q => q.Difficulty == Difficulty.Easy),
            questions.Count(q => q.Difficulty == Difficulty.Medium),
            questions.Count(q => q.Difficulty == Difficulty.Hard)));
        // Every row belongs to the literature quiz mode, and the other banks are untouched.
        var mode = await db.QuizModes.SingleAsync(m => m.Slug == QuizModeSlugs.EdebiyyatDunyasi);
        Assert.All(questions, q => Assert.Equal(mode.Id, q.QuizModeId));
        Assert.Equal(441, await db.Questions.CountAsync(q => q.Book!.Title == BilikYarisiSeed.BookTitle));
        Assert.Equal(30, await db.Questions.CountAsync(q => q.Book!.Title == OlulerQuestionSeed.BookTitle));
        // This bank brings no campaign of its own; an administrator opens one when the category goes live.
        Assert.Equal(2, await db.MonthlyCampaigns.CountAsync());
    }

    /// <summary>
    /// The workbook is the source of truth for this bank. A question reworded there has to replace the
    /// one in the database, not join it: the first version of this bank shipped 147 questions that were
    /// later rewritten, and a seeder that only inserts would have left both versions live, each with its
    /// own answer, on a category people are already playing.
    /// </summary>
    [Fact]
    public async Task Reseeding_makes_the_bank_match_the_workbook_again()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        int rewordedId;
        int editedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // One question as the previous workbook worded it: same bank, text no longer in the seed.
            var reworded = await db.Questions.FirstAsync(q => q.Book!.Title == EdebiyyatDunyasiSeed.BookTitle && q.ImageUrl != null);
            reworded.Text = "Köhnə redaksiyada verilmiş sual mətni";
            reworded.ImageUrl = "/question-images/literature-001.webp";
            rewordedId = reworded.Id;

            // And one whose wording still matches, but whose options and picture drifted.
            var edited = await db.Questions.FirstAsync(q => q.Book!.Title == EdebiyyatDunyasiSeed.BookTitle && q.ImageUrl == null);
            edited.OptionD = "Bazada dəyişdirilmiş cavab";
            edited.Explanation = "Bazada dəyişdirilmiş izah";
            edited.Category = "Səhv kateqoriya";
            editedId = edited.Id;

            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var check = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bank = EdebiyyatDunyasiSeed.LoadQuestions();

        // The bank is exactly the workbook: no leftovers, no duplicates.
        var rows = await check.Questions.Where(q => q.Book!.Title == EdebiyyatDunyasiSeed.BookTitle).ToListAsync();
        Assert.Equal(150, rows.Count);
        Assert.Null(await check.Questions.FirstOrDefaultAsync(q => q.Id == rewordedId));
        Assert.Equal(
            bank.Select(q => q.Text.Trim()).OrderBy(t => t, StringComparer.Ordinal),
            rows.Select(q => q.Text).OrderBy(t => t, StringComparer.Ordinal));

        // The row whose wording survived was corrected in place, keeping its id.
        var restored = await check.Questions.SingleAsync(q => q.Id == editedId);
        var source = bank.Single(q => q.Text.Trim() == restored.Text);
        Assert.Equal((source.OptionD, source.Explanation, source.Category), (restored.OptionD, restored.Explanation, restored.Category));

        // Nothing outside this book moved.
        Assert.Equal(441, await check.Questions.CountAsync(q => q.Book!.Title == BilikYarisiSeed.BookTitle));
        Assert.Equal(30, await check.Questions.CountAsync(q => q.Book!.Title == OlulerQuestionSeed.BookTitle));
    }

    // --- sessions ------------------------------------------------------------

    [Fact]
    public async Task Sessions_get_ten_literature_questions_3_4_3_with_the_campaign_image_target_and_no_leak()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await DbSeeder.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        int campaignId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // The campaign an administrator would open for this category: bookless, two illustrations per quiz.
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var campaign = new MonthlyCampaign
            {
                QuizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.EdebiyyatDunyasi),
                StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
                EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
                PassingScore = 7,
                RewardTitle = "Test mükafatı",
                ImageQuestionsPerQuiz = 2,
            };
            db.MonthlyCampaigns.Add(campaign);
            await db.SaveChangesAsync();
            campaignId = campaign.Id;
        }

        var literatureTexts = EdebiyyatDunyasiSeed.LoadQuestions().Select(q => q.Text.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var session = 0; session < 8; session++)
        {
            using var start = await client.PostAsJsonAsync("/api/game/start", new
            {
                fullName = "Ədəbiyyat Testi",
                phoneNumber = $"+9945070000{session:00}",
                campaignId,
            });
            var startRaw = await start.Content.ReadAsStringAsync();
            Assert.True(start.StatusCode == HttpStatusCode.OK, startRaw);
            AssertNoAnswerLeak(startRaw);

            var body = JsonDocument.Parse(startRaw).RootElement;
            Assert.Equal("edebiyyat-dunyasi", body.GetProperty("quizMode").GetProperty("slug").GetString());
            var sessionId = body.GetProperty("sessionId").GetGuid();
            var question = body.GetProperty("question");

            var seen = new List<(int Id, string Text, string Difficulty, string? Image)>();
            for (var number = 1; number <= 10; number++)
            {
                seen.Add((
                    question.GetProperty("id").GetInt32(),
                    question.GetProperty("text").GetString()!,
                    question.GetProperty("difficulty").GetString()!,
                    question.GetProperty("imageUrl").ValueKind == JsonValueKind.Null ? null : question.GetProperty("imageUrl").GetString()));

                using var answer = await client.PostAsJsonAsync($"/api/game/{sessionId}/answer",
                    new { questionId = question.GetProperty("id").GetInt32(), selectedOption = "A" });
                var raw = await answer.Content.ReadAsStringAsync();
                Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
                var result = JsonDocument.Parse(raw).RootElement;
                if (number < 10)
                {
                    AssertNoAnswerLeak(raw);
                    question = result.GetProperty("nextQuestion");
                }
                else
                {
                    Assert.True(result.GetProperty("isGameOver").GetBoolean());
                    Assert.Equal(20, result.GetProperty("result").GetProperty("maxPoints").GetInt32());
                }
            }

            Assert.Equal(10, seen.Select(q => q.Id).Distinct().Count());
            Assert.Equal((3, 4, 3), (
                seen.Count(q => q.Difficulty == "Easy"),
                seen.Count(q => q.Difficulty == "Medium"),
                seen.Count(q => q.Difficulty == "Hard")));
            // Only this bank's questions are drawn, and the campaign's image target is honoured.
            Assert.All(seen, q => Assert.Contains(q.Text.Trim(), literatureTexts));
            Assert.Equal(2, seen.Count(q => q.Image != null));
            Assert.All(seen.Where(q => q.Image != null), q => Assert.Matches(@"^/question-images/literature-\d{3}\.webp$", q.Image!));
        }
    }

    private static void AssertNoAnswerLeak(string raw)
    {
        foreach (var forbidden in new[] { "correctOption", "explanation", "isCorrect", "pointsEarned", "correctAnswers", "imageSource", "imageLicense" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
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
