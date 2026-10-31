using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// Demo campaign "Bilik yarışı": a mixed general-knowledge bank (flags, capitals, Azerbaijan, general
/// knowledge, Azerbaijani cinema) that reuses the existing Book + MonthlyCampaign model.
///
/// Source of truth: docs/import/quiz_questions.xlsx, converted at implementation time by
/// tools/import/convert_quiz_questions.py into Seed/Data/bilik-yarisi-questions.json (embedded resource).
/// The workbook is never read at runtime. SourceId is kept in the JSON for review only; SourceUrl, Notes
/// and Status are not carried at all. Rows marked "Yoxlanılmalı" were left out by the converter.
///
/// Idempotency (SourceId is not stored): a text question is identified by BookId + exact Text, an
/// illustrated flag question by BookId + exact Text + ImageUrl (all 100 flag questions share one text).
/// Existing rows are never updated, so administrator edits survive restarts.
///
/// The book, missing questions, the new campaign and the switch-off of the competing campaign run in one
/// transaction. The campaign is created (and the previous one disabled) only once, after the stored bank
/// has been verified; any failure rolls everything back and leaves the active campaign as it was.
/// </summary>
public static class BilikYarisiSeed
{
    public const string BookTitle = "Bilik yarışı";
    public const string BookAuthor = "Bakı Abadlıq Xidməti MMC";
    public const string BookDescription =
        "Dünya bayraqları, paytaxtlar, Azərbaycan, ümumi biliklər və Azərbaycan kinosu üzrə qarışıq bilik yarışı.";

    public static readonly DateOnly CampaignStart = new(2026, 9, 15);
    public static readonly DateOnly CampaignEnd = new(2026, 9, 30);
    public const int PassingScore = 8;
    public const string RewardTitle = "Bakı Abadlıq Xidməti MMC-dən hədiyyə";

    public const int ExpectedQuestionCount = 441;
    public const int ExpectedImageQuestionCount = 100;
    public const string ImageCategory = "Flags";

    public static readonly IReadOnlyDictionary<string, int> ExpectedPerCategory = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Flags"] = 100,
        ["Capitals"] = 100,
        ["Azerbaijan"] = 83,
        ["GeneralKnowledge"] = 119,
        ["AzerbaijaniCinema"] = 39,
    };

    private const string ResourceName = "LiteratureMillionaire.API.Seed.Data.bilik-yarisi-questions.json";
    private static readonly Regex FlagImageUrl = new(@"^/question-images/flag-\d{3}\.webp$", RegexOptions.CultureInvariant);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>One reviewed row of the bank, as stored in the embedded JSON.</summary>
    public sealed record SeedQuestion(
        string SourceId,
        string Category,
        Difficulty Difficulty,
        string Text,
        string OptionA,
        string OptionB,
        string OptionC,
        string OptionD,
        string CorrectOption,
        string? Explanation,
        string? ImageUrl,
        string? ImageAltText)
    {
        public string[] Options => new[] { OptionA, OptionB, OptionC, OptionD };
        public string Key => IdempotencyKey(Text, ImageUrl);
    }

    /// <summary>BookId-scoped identity: exact text, plus the image URL for illustrated questions.</summary>
    public static string IdempotencyKey(string text, string? imageUrl) =>
        imageUrl is null ? text : text + "" + imageUrl;

    public static IReadOnlyList<SeedQuestion> LoadQuestions()
    {
        using var stream = typeof(BilikYarisiSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded seed resource '{ResourceName}' is missing.");
        return JsonSerializer.Deserialize<List<SeedQuestion>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Embedded seed resource '{ResourceName}' is empty.");
    }

    /// <summary>Structural and content checks of the bank. Returns every problem found; empty means valid.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<SeedQuestion> questions)
    {
        var problems = new List<string>();

        if (questions.Count != ExpectedQuestionCount)
            problems.Add($"Expected {ExpectedQuestionCount} questions, found {questions.Count}.");

        var perCategory = questions.GroupBy(q => q.Category, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        foreach (var (category, expected) in ExpectedPerCategory)
        {
            var actual = perCategory.GetValueOrDefault(category);
            if (actual != expected) problems.Add($"Category {category}: expected {expected}, found {actual}.");
        }
        problems.AddRange(perCategory.Keys.Where(c => !ExpectedPerCategory.ContainsKey(c)).Select(c => $"Unexpected category '{c}'."));

        foreach (var q in questions)
        {
            var id = q.SourceId;
            if (string.IsNullOrWhiteSpace(id)) problems.Add("Question without SourceId.");
            if (string.IsNullOrWhiteSpace(q.Text) || q.Text.Length > 1000) problems.Add($"{id}: text is empty or longer than 1000.");
            if (q.Options.Any(o => string.IsNullOrWhiteSpace(o) || o.Length > 300)) problems.Add($"{id}: an option is empty or longer than 300.");
            if (q.Options.Select(o => o.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4) problems.Add($"{id}: options are not four distinct values.");
            if (q.CorrectOption is not ("A" or "B" or "C" or "D")) problems.Add($"{id}: CorrectOption '{q.CorrectOption}' is not A-D.");
            if (!Enum.IsDefined(q.Difficulty)) problems.Add($"{id}: undefined difficulty.");
            if (q.Explanation is { Length: > 2000 }) problems.Add($"{id}: explanation longer than 2000.");

            if (q.Category == ImageCategory)
            {
                if (q.ImageUrl is null || !FlagImageUrl.IsMatch(q.ImageUrl)) problems.Add($"{id}: flag question needs ImageUrl /question-images/flag-NNN.webp.");
                if (string.IsNullOrWhiteSpace(q.ImageAltText) || q.ImageAltText.Length > 300) problems.Add($"{id}: flag question needs an alt text of at most 300 characters.");
                else if (q.CorrectOption is ("A" or "B" or "C" or "D")
                         && q.ImageAltText.Contains(q.Options["ABCD".IndexOf(q.CorrectOption[0])].Trim(), StringComparison.OrdinalIgnoreCase))
                    problems.Add($"{id}: alt text reveals the correct answer.");
            }
            else if (q.ImageUrl is not null || q.ImageAltText is not null)
            {
                problems.Add($"{id}: only {ImageCategory} questions carry an image.");
            }
        }

        problems.AddRange(questions.GroupBy(q => q.SourceId, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => $"Duplicate SourceId {g.Key}."));
        problems.AddRange(questions.GroupBy(q => q.Key, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => $"Duplicate question (text/image): {g.First().SourceId}."));

        var images = questions.Where(q => q.ImageUrl is not null).Select(q => q.ImageUrl!).ToList();
        if (images.Count != ExpectedImageQuestionCount || images.Distinct(StringComparer.Ordinal).Count() != images.Count)
            problems.Add($"Expected {ExpectedImageQuestionCount} distinct image URLs, found {images.Distinct(StringComparer.Ordinal).Count()} of {images.Count}.");

        var pool = questions.Select((q, i) => new PoolQuestion(i, 'A', q.Difficulty, q.ImageUrl is not null)).ToList();
        problems.AddRange(QuestionMixPlanner.Shortfalls(pool).Select(d => $"Not enough {d} questions for a session."));

        return problems;
    }

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var seed = LoadQuestions();
        var problems = Validate(seed);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Bilik yarışı seed data is invalid:\n" + string.Join("\n", problems));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.BilikDunyasi, ct);

        var book = await db.Books.FirstOrDefaultAsync(b => b.Title == BookTitle, ct);
        if (book is null)
        {
            book = new Book
            {
                Title = BookTitle,
                Author = BookAuthor,
                Description = BookDescription,
                CoverImageUrl = string.Empty, // no cover: the column is non-nullable; the kiosk shows its designed placeholder
                IsActive = true
            };
            db.Books.Add(book);
            await db.SaveChangesAsync(ct);
        }


        // The panel owns this bank once it has questions (see BankOwnership): filled here once, never
        // reconciled again, so an edit or a deletion made in the editor survives every deployment.
        if (await BankOwnership.AlreadyFilledAsync(db, book.Id, ct))
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var existingKeys = (await db.Questions
                .Where(q => q.BookId == book.Id)
                .Select(q => new { q.Text, q.ImageUrl })
                .ToListAsync(ct))
            .Select(q => IdempotencyKey(q.Text, q.ImageUrl))
            .ToHashSet(StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var missing = seed
            .Where(s => !existingKeys.Contains(s.Key))
            .Select(s => new Question
            {
                BookId = book.Id,
                QuizModeId = quizModeId,
                Text = s.Text,
                OptionA = s.OptionA,
                OptionB = s.OptionB,
                OptionC = s.OptionC,
                OptionD = s.OptionD,
                CorrectOption = s.CorrectOption[0],
                Difficulty = s.Difficulty,
                Category = s.Category,
                Explanation = s.Explanation,
                ImageUrl = s.ImageUrl,
                ImageAltText = s.ImageAltText,
                CreatedAt = now
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Questions.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }

        // Rows of this book without a mode (created before quiz modes) are played in "Bilik Dünyası"; an assigned mode is never changed.
        await db.Questions
            .Where(q => q.BookId == book.Id && q.QuizModeId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(q => q.QuizModeId, (int?)quizModeId), ct);

        // Any campaign of the book, whatever its dates: once one exists, campaigns are the admin panel's, and a date
        // changed there must not bring the seeded one back on the next deployment.
        var campaignExists = await db.MonthlyCampaigns.AnyAsync(c => c.BookId == book.Id, ct);
        if (!campaignExists)
        {
            await VerifyStoredBankAsync(db, book.Id, seed, ct);

            // First import only: switch off other enabled campaigns overlapping these dates (on the production import this
            // was the "Ölülər" campaign). Quiz modes now allow campaigns of different modes in parallel; re-enabling one
            // is an administrator's decision.
            var competing = await db.MonthlyCampaigns
                .Where(c => c.IsEnabled && c.BookId != book.Id && c.StartDate <= CampaignEnd && c.EndDate >= CampaignStart)
                .OrderBy(c => c.Id)
                .ToListAsync(ct);

            foreach (var campaign in competing)
            {
                campaign.IsEnabled = false;
            }

            db.MonthlyCampaigns.Add(new MonthlyCampaign
            {
                QuizModeId = quizModeId,
                BookId = book.Id,
                ImageQuestionsPerQuiz = QuizRules.DefaultImageQuestionsPerQuiz,
                StartDate = CampaignStart,
                EndDate = CampaignEnd,
                PassingScore = PassingScore,
                RewardTitle = RewardTitle,
                IsEnabled = true
            });
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <summary>Checks what is actually stored before the campaign goes live; throws (and so rolls back) on any gap.</summary>
    private static async Task VerifyStoredBankAsync(ApplicationDbContext db, int bookId, IReadOnlyList<SeedQuestion> seed, CancellationToken ct)
    {

        var stored = await db.Questions
            .Where(q => q.BookId == bookId)
            .Select(q => new { q.Id, q.Text, q.ImageUrl, q.Difficulty, q.CorrectOption })
            .ToListAsync(ct);

        var storedKeys = stored.Select(q => IdempotencyKey(q.Text, q.ImageUrl)).ToHashSet(StringComparer.Ordinal);
        var missingCount = seed.Count(s => !storedKeys.Contains(s.Key));
        var distinctImages = stored.Where(q => q.ImageUrl != null).Select(q => q.ImageUrl).Distinct().Count();
        var pool = stored.Select(q => new PoolQuestion(q.Id, q.CorrectOption, q.Difficulty, q.ImageUrl != null)).ToList();

        if (missingCount > 0 || distinctImages < ExpectedImageQuestionCount || QuestionMixPlanner.Shortfalls(pool).Count > 0)
        {
            throw new InvalidOperationException(
                $"Bilik yarışı bank verification failed (missing {missingCount}, distinct images {distinctImages}); the active campaign was not changed.");
        }
    }
}
