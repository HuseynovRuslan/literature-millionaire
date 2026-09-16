using System.Text.Json;
using System.Text.Json.Serialization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// The reviewed "Ədəbiyyat Dünyası" bank: 150 questions on Azerbaijani and world literature, 30 of them
/// illustrated with portraits and monuments.
///
/// Source of truth: docs/edebiyyat-import/edebiyyat-dunyasi.xlsx, converted at implementation time by
/// tools/import/convert_edebiyyat_questions.py into Seed/Data/edebiyyat-dunyasi-questions.json (embedded
/// resource). The workbook is never read at runtime. Only rows the reviewer marked "Təsdiqlənib" are
/// carried; the per-question source URL and the reviewer's notes stay in the workbook.
///
/// Idempotency (SourceId is not stored): a question is identified by BookId + its exact text, which the
/// workbook keeps unique. Existing rows are never updated, so an administrator's edits survive restarts.
/// The book and the missing questions are written in one transaction.
///
/// No campaign is created here: when the category goes live, an administrator opens a campaign for the
/// "edebiyyat-dunyasi" quiz mode with the dates, passing score and image target they want.
/// </summary>
public static class EdebiyyatDunyasiSeed
{
    public const string BookTitle = "Ədəbiyyat Dünyası";
    public const string BookAuthor = "Bakı Abadlıq Xidməti MMC";
    public const string BookDescription = "Azərbaycan və dünya ədəbiyyatı üzrə bilik yarışı: klassik poeziya, nəsr, dramaturgiya, folklor və ədəbi mühit.";

    public const int ExpectedQuestionCount = 150;
    public const int ExpectedImageQuestionCount = 30;

    public static readonly IReadOnlyDictionary<Difficulty, int> ExpectedPerDifficulty = new Dictionary<Difficulty, int>
    {
        [Difficulty.Easy] = 45,
        [Difficulty.Medium] = 60,
        [Difficulty.Hard] = 45,
    };

    private const string ResourceName = "LiteratureMillionaire.API.Seed.Data.edebiyyat-dunyasi-questions.json";

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
        string? ImageAltText,
        string? ImageSource,
        string? ImageLicense)
    {
        public string[] Options => new[] { OptionA, OptionB, OptionC, OptionD };
    }

    private sealed record BankFile(IReadOnlyList<SeedQuestion> Questions);

    /// <summary>Reads the embedded bank. Throws when it is missing or unreadable.</summary>
    public static IReadOnlyList<SeedQuestion> LoadQuestions()
    {
        using var stream = typeof(EdebiyyatDunyasiSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded question bank '{ResourceName}' was not found.");
        var file = JsonSerializer.Deserialize<BankFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Ədəbiyyat Dünyası bank could not be read.");
        return file.Questions;
    }

    /// <summary>Problems that make the bank unusable. Empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<SeedQuestion> questions)
    {
        var problems = new List<string>();

        if (questions.Count != ExpectedQuestionCount)
        {
            problems.Add($"Expected {ExpectedQuestionCount} questions, found {questions.Count}.");
        }

        foreach (var (difficulty, expected) in ExpectedPerDifficulty)
        {
            var actual = questions.Count(q => q.Difficulty == difficulty);
            if (actual != expected)
            {
                problems.Add($"Expected {expected} {difficulty} questions, found {actual}.");
            }
        }

        var illustrated = questions.Where(q => q.ImageUrl is not null).ToList();
        if (illustrated.Count != ExpectedImageQuestionCount)
        {
            problems.Add($"Expected {ExpectedImageQuestionCount} illustrated questions, found {illustrated.Count}.");
        }

        foreach (var duplicate in questions.GroupBy(q => q.SourceId, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate SourceId {duplicate.Key}.");
        }

        // The text is the idempotency key, so two questions may never share one.
        foreach (var duplicate in questions.GroupBy(q => q.Text.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate question text: {Shorten(duplicate.Key)}");
        }

        foreach (var duplicate in illustrated.GroupBy(q => q.ImageUrl!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate image {duplicate.Key}.");
        }

        foreach (var question in questions)
        {
            if (string.IsNullOrWhiteSpace(question.Text)) problems.Add($"{question.SourceId}: question text is empty.");
            if (question.CorrectOption is not ("A" or "B" or "C" or "D")) problems.Add($"{question.SourceId}: correct option '{question.CorrectOption}' is not A-D.");
            if (question.Options.Any(string.IsNullOrWhiteSpace) || question.Options.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                problems.Add($"{question.SourceId}: options are not four distinct values.");
            }

            if (question.ImageUrl is not null)
            {
                if (!question.ImageUrl.StartsWith("/question-images/literature-", StringComparison.Ordinal))
                {
                    problems.Add($"{question.SourceId}: image '{question.ImageUrl}' is not a literature asset.");
                }
                if (string.IsNullOrWhiteSpace(question.ImageAltText)) problems.Add($"{question.SourceId}: illustrated question has no alt text.");
                // Attribution has to travel with a picture that is published under a licence requiring it.
                if (string.IsNullOrWhiteSpace(question.ImageSource)) problems.Add($"{question.SourceId}: illustrated question has no image source.");
                if (string.IsNullOrWhiteSpace(question.ImageLicense)) problems.Add($"{question.SourceId}: illustrated question has no image licence.");

                var answer = question.Options["ABCD".IndexOf(question.CorrectOption[0])];
                if (question.ImageAltText is { } alt && alt.Contains(answer, StringComparison.CurrentCultureIgnoreCase))
                {
                    problems.Add($"{question.SourceId}: alt text names the answer.");
                }
            }
            else if (question.ImageAltText is not null || question.ImageSource is not null || question.ImageLicense is not null)
            {
                problems.Add($"{question.SourceId}: text-only question carries image metadata.");
            }
        }

        return problems;
    }

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var seed = LoadQuestions();
        var problems = Validate(seed);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Ədəbiyyat Dünyası seed data is invalid:\n" + string.Join("\n", problems));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.EdebiyyatDunyasi, ct);

        var book = await db.Books.FirstOrDefaultAsync(b => b.Title == BookTitle, ct);
        if (book is null)
        {
            book = new Book
            {
                Title = BookTitle,
                Author = BookAuthor,
                Description = BookDescription,
                CoverImageUrl = string.Empty,
                IsActive = true,
            };
            db.Books.Add(book);
            await db.SaveChangesAsync(ct);
        }

        var existingTexts = (await db.Questions
                .Where(q => q.BookId == book.Id)
                .Select(q => q.Text)
                .ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        var missing = seed
            .Where(q => !existingTexts.Contains(q.Text.Trim()))
            .Select(q => new Question
            {
                BookId = book.Id,
                QuizModeId = quizModeId,
                Text = q.Text.Trim(),
                OptionA = q.OptionA,
                OptionB = q.OptionB,
                OptionC = q.OptionC,
                OptionD = q.OptionD,
                CorrectOption = q.CorrectOption[0],
                Difficulty = q.Difficulty,
                Category = q.Category,
                Explanation = q.Explanation,
                ImageUrl = q.ImageUrl,
                ImageAltText = q.ImageAltText,
                ImageSource = q.ImageSource,
                ImageLicense = q.ImageLicense,
                CreatedAt = now,
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Questions.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }

        // Rows of this book created before quiz modes existed are played in "Ədəbiyyat Dünyası";
        // an assigned mode is never changed.
        await db.Questions
            .Where(q => q.BookId == book.Id && q.QuizModeId == null)
            .ExecuteUpdateAsync(set => set.SetProperty(q => q.QuizModeId, (int?)quizModeId), ct);

        await transaction.CommitAsync(ct);
    }

    private static string Shorten(string value) => value.Length <= 60 ? value : value[..57] + "...";
}
