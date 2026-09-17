using System.Text.Json;
using System.Text.Json.Serialization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// The "Green Garden Kolleksiyası" bank: the nursery's own catalogue, one question per entry, each with
/// the picture the catalogue prints of it.
///
/// Where it comes from. docs/greegardenbitkiler-import - the catalogue converted page by page, its Latin
/// name and page number carried with every row - and tools/import/build_green_garden_questions.py, which
/// turns that into this bank. Neither is read at runtime.
///
/// What is trustworthy here and what is not. The catalogue prints Latin only, so the scientific name is
/// sourced and the Azerbaijani name is not: not one of the 272 rows arrived marked "from the book". The
/// Azerbaijani half of a label therefore survives only where Wikidata and az.wikipedia carry a name for
/// that species; everywhere else the option stands on its Latin name alone, which is both true and what
/// a nursery actually calls the plant. Nothing on a button is a guess.
///
/// Cultivars stay as separate questions, because in a catalogue the cultivar IS the plant - a landscaper
/// orders 'Blue Chip', not Juniperus horizontalis. But thirteen Acer palmatum cultivars are one
/// photograph thirteen times over to anybody's eye, so no two rows of the same genus are ever offered
/// against each other. A test enforces that rather than trusting the generator.
///
/// The pictures are the catalogue's own and are credited to it by page. Whether Green Garden has agreed
/// to their being shown here is a question for whoever owns that relationship, not something this file
/// can assert - so the licence field records the source and claims no permission.
///
/// Reconciliation: a question is identified by BookId + ImageUrl, which is unique because one catalogue
/// entry makes exactly one question (the wording is the same on all of them, so it cannot be the key).
/// Stale rows removed, changed rows updated, new rows inserted, in one transaction, and only if the
/// result is exactly ExpectedQuestionCount rows.
///
/// The opening campaign is created once, because a category with no campaign is invisible - the home page
/// lists what can be played, not what exists. Its dates and IsEnabled afterwards belong to whoever
/// administers them and survive every deployment.
/// </summary>
public static class GreenGardenSeed
{
    public const string BookTitle = "Green Garden Kolleksiyası";
    public const string BookAuthor = "Green Garden";
    public const string BookDescription =
        "Green Garden bitki kataloqu: bəzək ağacları, kollar, sarmaşıqlar və onların sortları.";

    public const int ExpectedQuestionCount = 272;

    private static readonly DateOnly CampaignStart = new(2026, 9, 17);
    private static readonly DateOnly CampaignEnd = new(2026, 9, 30);
    private const int PassingScore = 8;
    private const string RewardTitle = "Bakı Abadlıq Xidməti MMC-dən hədiyyə";

    private const string ImagePrefix = "/question-images/gg-";
    private const string ResourceName = "LiteratureMillionaire.API.Seed.Data.green-garden-questions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>One generated question, as stored in the embedded JSON.</summary>
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
        string ImageUrl,
        string ImageAltText,
        string ImageSource,
        string ImageLicense)
    {
        public string[] Options => new[] { OptionA, OptionB, OptionC, OptionD };

        public string Answer => Options["ABCD".IndexOf(CorrectOption[0], StringComparison.Ordinal)];

        /// <summary>The genus, which every option opens with: the one word two options may not share.</summary>
        public static string GenusOf(string option) => option.Split(' ')[0];
    }

    private sealed record BankFile(IReadOnlyList<SeedQuestion> Questions);

    /// <summary>Reads the embedded bank. Throws when it is missing or unreadable.</summary>
    public static IReadOnlyList<SeedQuestion> LoadQuestions()
    {
        using var stream = typeof(GreenGardenSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded question bank '{ResourceName}' was not found.");
        var file = JsonSerializer.Deserialize<BankFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Green Garden bank could not be read.");
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

        foreach (var difficulty in Enum.GetValues<Difficulty>())
        {
            var actual = questions.Count(q => q.Difficulty == difficulty);
            if (actual < QuizRules.QuestionsPerQuiz)
            {
                problems.Add($"Only {actual} {difficulty} questions; a round cannot be filled.");
            }
        }

        foreach (var duplicate in questions.GroupBy(q => q.SourceId, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate SourceId {duplicate.Key}.");
        }

        // The picture is the idempotency key, so two questions may never share one.
        foreach (var duplicate in questions.GroupBy(q => q.ImageUrl, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate image {duplicate.Key}.");
        }

        // Two entries that ended up with the same label are one unanswerable question however it is worded.
        foreach (var duplicate in questions.GroupBy(q => q.Answer, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Add($"Two questions answer to the same name: {duplicate.Key}.");
        }

        foreach (var question in questions)
        {
            var id = question.SourceId;

            if (string.IsNullOrWhiteSpace(question.Text)) problems.Add($"{id}: question text is empty.");
            if (question.CorrectOption is not ("A" or "B" or "C" or "D")) problems.Add($"{id}: correct option '{question.CorrectOption}' is not A-D.");
            if (question.Options.Any(string.IsNullOrWhiteSpace) || question.Options.Distinct(StringComparer.Ordinal).Count() != 4)
            {
                problems.Add($"{id}: options are not four distinct values.");
            }

            // A cultivar photographed in a pot looks like every other cultivar of its genus in a pot.
            var genus = SeedQuestion.GenusOf(question.Answer);
            if (question.Options.Count(o => SeedQuestion.GenusOf(o) == genus) > 1)
            {
                problems.Add($"{id}: two options share the genus {genus}.");
            }

            if (!question.ImageUrl.StartsWith(ImagePrefix, StringComparison.Ordinal)
                || !question.ImageUrl.EndsWith(".webp", StringComparison.Ordinal))
            {
                problems.Add($"{id}: image '{question.ImageUrl}' is not a Green Garden asset.");
            }

            if (string.IsNullOrWhiteSpace(question.ImageAltText) || question.ImageAltText.Length > 300) problems.Add($"{id}: alt text is missing or too long.");
            // The catalogue is somebody's work even when that somebody said yes; the page travels with it.
            if (string.IsNullOrWhiteSpace(question.ImageSource)) problems.Add($"{id}: no image source.");
            if (string.IsNullOrWhiteSpace(question.ImageLicense)) problems.Add($"{id}: no image licence.");

            // The alt text is read aloud before the options, so it may never name the plant.
            if (question.ImageAltText.Contains(question.Answer, StringComparison.CurrentCultureIgnoreCase))
            {
                problems.Add($"{id}: alt text names the answer.");
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
            throw new InvalidOperationException("Green Garden seed data is invalid:\n" + string.Join("\n", problems));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var quizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.GreenGarden, ct);

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

        var existing = await db.Questions.Where(q => q.BookId == book.Id).ToListAsync(ct);
        var byImage = new Dictionary<string, Question>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in existing)
        {
            // A row of this book without a picture, or a second row on the same picture, can only come
            // from outside this seeder; it stays unmatched and is removed below.
            if (row.ImageUrl is { } url)
            {
                byImage.TryAdd(url, row);
            }
        }

        var now = DateTime.UtcNow;
        var keep = new HashSet<int>();

        foreach (var q in seed)
        {
            if (byImage.TryGetValue(q.ImageUrl, out var row))
            {
                keep.Add(row.Id);
                Apply(row, q, quizModeId);
                continue;
            }

            var added = new Question { BookId = book.Id, CreatedAt = now };
            Apply(added, q, quizModeId);
            db.Questions.Add(added);
        }

        var stale = existing.Where(row => !keep.Contains(row.Id)).ToList();
        if (stale.Count > 0)
        {
            db.Questions.RemoveRange(stale);
        }

        await db.SaveChangesAsync(ct);

        var total = await db.Questions.CountAsync(q => q.BookId == book.Id, ct);
        if (total != ExpectedQuestionCount)
        {
            // Rolls the whole reconciliation back rather than leave the bank in a state nobody reviewed.
            throw new InvalidOperationException(
                $"After reconciling, '{BookTitle}' holds {total} questions, expected {ExpectedQuestionCount}.");
        }

        // Every question here carries a picture, so a round is ten of them.
        // Any campaign of the book, whatever its dates: once one exists, campaigns are the admin panel's, and a date
        // changed there must not bring the seeded one back on the next deployment.
        var campaignExists = await db.MonthlyCampaigns.AnyAsync(c => c.BookId == book.Id, ct);
        if (!campaignExists)
        {
            db.MonthlyCampaigns.Add(new MonthlyCampaign
            {
                QuizModeId = quizModeId,
                BookId = book.Id,
                ImageQuestionsPerQuiz = QuizRules.QuestionsPerQuiz,
                StartDate = CampaignStart,
                EndDate = CampaignEnd,
                PassingScore = PassingScore,
                RewardTitle = RewardTitle,
                IsEnabled = true,
            });
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    /// <summary>Copies one generated row onto a question, leaving Id, BookId and CreatedAt alone.</summary>
    private static void Apply(Question row, SeedQuestion q, int quizModeId)
    {
        row.QuizModeId = quizModeId;
        row.Text = q.Text.Trim();
        row.OptionA = q.OptionA;
        row.OptionB = q.OptionB;
        row.OptionC = q.OptionC;
        row.OptionD = q.OptionD;
        row.CorrectOption = q.CorrectOption[0];
        row.Difficulty = q.Difficulty;
        row.Category = q.Category;
        row.Explanation = q.Explanation;
        row.ImageUrl = q.ImageUrl;
        row.ImageAltText = q.ImageAltText;
        row.ImageSource = q.ImageSource;
        row.ImageLicense = q.ImageLicense;
    }
}
