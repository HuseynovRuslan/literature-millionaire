using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// Seeds a small starter set of Azerbaijani literature questions on first run.
/// Runs only when the Questions table is empty, so it is safe to call on every startup.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        // Quiz modes first: campaigns and per-mode questions reference them.
        await QuizModeSeed.SeedAsync(db, ct);

        var questions = StarterQuestions().ToList();
        Validate(questions);

        // Idempotent by question text: rows already present (from an earlier seed run)
        // are skipped, user-created rows are never touched, new demo rows are added.
        var existingTexts = (await db.Questions.Select(q => q.Text).ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = questions.Where(q => !existingTexts.Contains(q.Text.Trim())).ToList();
        if (missing.Count > 0)
        {
            db.Questions.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }

        await SeedCampaignAsync(db, ct);

        // Approved per-book content (guarded by BookId + exact text inside).
        await OlulerQuestionSeed.SeedAsync(db, ct);

        // Demo "Bilik yarışı" bank + campaign. Runs after "Ölülər" so, on first import, it can switch the
        // "Ölülər" campaign off; the "Ölülər" book, questions and campaign row are kept (transactional, idempotent).
        await BilikYarisiSeed.SeedAsync(db, ct);
    }

    /// <summary>
    /// Development "Book of the Month" demo: one book and one campaign for September 2026.
    /// Guarded by book title and campaign start date, so re-running never duplicates rows.
    /// </summary>
    private static async Task SeedCampaignAsync(ApplicationDbContext db, CancellationToken ct)
    {
        const string bookTitle = "Ölülər";
        var campaignStart = new DateOnly(2026, 9, 1);

        var book = await db.Books.FirstOrDefaultAsync(b => b.Title == bookTitle, ct);
        if (book is null)
        {
            book = new Book
            {
                Title = bookTitle,
                Author = "Cəlil Məmmədquluzadə",
                Description = "1909-cu ildə yazılmış tragikomediya. Əsər mövhumat və cəhalətin hökm sürdüyü bir mühitdə "
                            + "özünü \"Şeyx Nəsrullah\" adlandıran fırıldaqçının ölüləri diriltmək vədi ilə insanları aldatmasını, "
                            + "yeganə ayıq insan olan İsgəndərin isə \"dəli\" sayılmasını göstərir.",
                CoverImageUrl = "/covers/oluler.webp",
                IsActive = true
            };
            db.Books.Add(book);
            await db.SaveChangesAsync(ct);
        }

        var hasCampaign = await db.MonthlyCampaigns.AnyAsync(c => c.BookId == book.Id && c.StartDate == campaignStart, ct);
        if (hasCampaign)
        {
            return;
        }

        db.MonthlyCampaigns.Add(new MonthlyCampaign
        {
            QuizModeId = await QuizModeSeed.GetIdAsync(db, QuizModeSlugs.AyinKitabi, ct),
            BookId = book.Id,
            StartDate = campaignStart,
            EndDate = new DateOnly(2026, 9, 30),
            PassingScore = 8,
            RewardTitle = "[DEV] Müvəqqəti mükafat – sentyabr 2026",
            IsEnabled = true
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Structural guard so a typo in the seed list fails loudly at startup
    /// instead of producing an unanswerable question.
    /// </summary>
    private static void Validate(IReadOnlyCollection<Question> questions)
    {
        var problems = new List<string>();

        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q.Text)) problems.Add("Empty question text.");
            if (string.IsNullOrWhiteSpace(q.OptionA) || string.IsNullOrWhiteSpace(q.OptionB) ||
                string.IsNullOrWhiteSpace(q.OptionC) || string.IsNullOrWhiteSpace(q.OptionD))
                problems.Add($"Empty option in: {q.Text}");
            if (q.CorrectOption is not ('A' or 'B' or 'C' or 'D'))
                problems.Add($"CorrectOption '{q.CorrectOption}' is not A-D in: {q.Text}");
            if (!Enum.IsDefined(q.Difficulty))
                problems.Add($"Undefined difficulty in: {q.Text}");
            if (string.IsNullOrWhiteSpace(q.Category))
                problems.Add($"Empty category in: {q.Text}");
        }

        var duplicates = questions
            .GroupBy(q => q.Text.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);
        problems.AddRange(duplicates.Select(t => $"Duplicate question: {t}"));

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Seed data is invalid:\n" + string.Join("\n", problems));
        }
    }

    private static IEnumerable<Question> StarterQuestions()
    {
        var now = DateTime.UtcNow;

        yield return Q(now, Difficulty.Easy, "Klassik poeziya",
            "\"Leyli və Məcnun\" poemasının Azərbaycan dilində müəllifi kimdir?",
            "Nizami Gəncəvi", "Məhəmməd Füzuli", "İmadəddin Nəsimi", "Xaqani Şirvani", 'B',
            "Füzuli bu poemanı 1536-cı ildə Azərbaycan dilində yazmışdır. Nizaminin eyniadlı poeması fars dilindədir.");

        yield return Q(now, Difficulty.Easy, "Şifahi xalq ədəbiyyatı",
            "\"Kitabi-Dədə Qorqud\" dastanı neçə boydan ibarətdir?",
            "7", "10", "12", "15", 'C',
            "Dastan bir müqəddimə və 12 boydan ibarətdir.");

        yield return Q(now, Difficulty.Easy, "Dramaturgiya",
            "\"Ölülər\" tragikomediyasının müəllifi kimdir?",
            "Mirzə Fətəli Axundzadə", "Cəlil Məmmədquluzadə", "Nəcəf bəy Vəzirov", "Hüseyn Cavid", 'B',
            "\"Ölülər\" 1909-cu ildə Cəlil Məmmədquluzadə tərəfindən yazılmışdır.");

        yield return Q(now, Difficulty.Medium, "Mətbuat tarixi",
            "\"Molla Nəsrəddin\" jurnalı hansı ildə nəşrə başlamışdır?",
            "1875", "1896", "1906", "1918", 'C',
            "İlk nömrə 7 aprel 1906-cı ildə Tiflisdə çıxmışdır.");

        yield return Q(now, Difficulty.Medium, "Klassik poeziya",
            "Xaqani Şirvaninin məşhur \"Mədain xərabələri\" əsəri hansı janrdadır?",
            "Qəzəl", "Qəsidə", "Məsnəvi", "Rübai", 'B',
            "\"Mədain xərabələri\" fəlsəfi məzmunlu qəsidədir.");

        yield return Q(now, Difficulty.Medium, "Dramaturgiya",
            "Səməd Vurğunun \"Vaqif\" mənzum dramı hansı ildə yazılmışdır?",
            "1927", "1937", "1945", "1953", 'B',
            "Dram 1937-ci ildə yazılmış və 1938-ci ildə səhnəyə qoyulmuşdur.");

        yield return Q(now, Difficulty.Hard, "Ədəbiyyatşünaslıq",
            "\"Təzkireyi-Nəvvab\" əsərinin müəllifi kimdir?",
            "Mir Möhsün Nəvvab", "Seyid Əzim Şirvani", "Məhəmmədəli Tərbiyət", "Firidun bəy Köçərli", 'A',
            "Bu təzkirə Qarabağ şairləri haqqında məlumat verir.");

        yield return Q(now, Difficulty.Hard, "Klassik poeziya",
            "İmadəddin Nəsimi hansı şəhərdə edam edilmişdir?",
            "Bağdad", "Təbriz", "Hələb", "Şamaxı", 'C',
            "Nəsimi 1417-ci ildə Hələb şəhərində edam edilmişdir.");

        // --- Demo bank expansion (task 3): 3 Easy, 3 Medium, 4 Hard ---------

        yield return Q(now, Difficulty.Easy, "Klassik poeziya",
            "\"Xəmsə\" (\"Beşlik\") adlı beş poemadan ibarət məşhur əsərin müəllifi kimdir?",
            "Məhəmməd Füzuli", "Nizami Gəncəvi", "İmadəddin Nəsimi", "Molla Pənah Vaqif", 'B',
            "Nizami Gəncəvinin \"Xəmsə\"si beş poemadan ibarətdir: \"Sirlər xəzinəsi\", \"Xosrov və Şirin\", \"Leyli və Məcnun\", \"Yeddi gözəl\", \"İsgəndərnamə\".");

        yield return Q(now, Difficulty.Easy, "Dramaturgiya",
            "Azərbaycan dramaturgiyasının banisi kimdir?",
            "Cəlil Məmmədquluzadə", "Nəcəf bəy Vəzirov", "Mirzə Fətəli Axundzadə", "Əbdürrəhim bəy Haqverdiyev", 'C',
            "Mirzə Fətəli Axundzadə 1850-1855-ci illərdə yazdığı komediyaları ilə Azərbaycan dramaturgiyasının əsasını qoymuşdur.");

        yield return Q(now, Difficulty.Easy, "XVIII əsr poeziyası",
            "Molla Pənah Vaqif hansı xanlığın vəziri olmuşdur?",
            "Şəki xanlığı", "Quba xanlığı", "Gəncə xanlığı", "Qarabağ xanlığı", 'D',
            "Vaqif Qarabağ xanı İbrahimxəlil xanın sarayında vəzir vəzifəsində çalışmışdır.");

        yield return Q(now, Difficulty.Medium, "Nəsr",
            "\"Bahadır və Sona\" romanının müəllifi kimdir?",
            "Nəriman Nərimanov", "Cəlil Məmmədquluzadə", "Əbdürrəhim bəy Haqverdiyev", "Mirzə Ələkbər Sabir", 'A',
            "\"Bahadır və Sona\" Nəriman Nərimanovun 1896-1899-cu illərdə yazdığı romandır.");

        yield return Q(now, Difficulty.Medium, "Satirik poeziya",
            "\"Hophopnamə\" kimin şeirlər toplusudur?",
            "Seyid Əzim Şirvani", "Mirzə Ələkbər Sabir", "Xurşidbanu Natəvan", "Cəlil Məmmədquluzadə", 'B',
            "\"Hophopnamə\" Mirzə Ələkbər Sabirin satirik şeirlərinin toplusudur; ilk nəşri 1912-ci ildə çıxmışdır.");

        yield return Q(now, Difficulty.Medium, "Mətbuat tarixi",
            "Azərbaycanda ilk milli qəzet olan \"Əkinçi\" hansı ildə nəşrə başlamışdır?",
            "1832", "1875", "1906", "1920", 'B',
            "\"Əkinçi\" qəzeti 22 iyul 1875-ci ildə Həsən bəy Zərdabi tərəfindən Bakıda nəşr edilmişdir.");

        yield return Q(now, Difficulty.Hard, "Klassik poeziya",
            "\"Hədiqətüs-süəda\" əsərinin müəllifi kimdir?",
            "İmadəddin Nəsimi", "Şah İsmayıl Xətai", "Məhəmməd Füzuli", "Nemətullah Kişvəri", 'C',
            "\"Hədiqətüs-süəda\" (\"Xoşbəxtlər bağçası\") Füzulinin Kərbəla hadisələrindən bəhs edən nəsr əsəridir.");

        yield return Q(now, Difficulty.Hard, "Klassik poeziya",
            "Şah İsmayıl Xətainin \"Dəhnamə\" poeması neçə məktubdan ibarətdir?",
            "7", "10", "12", "15", 'B',
            "\"Dəhnamə\" adı \"On məktub\" deməkdir; poema aşiqin məşuqəyə yazdığı on məktub üzərində qurulub.");

        yield return Q(now, Difficulty.Hard, "Klassik poeziya",
            "Nizami Gəncəvinin \"Xəmsə\"sinə daxil olan ilk poema hansıdır?",
            "\"Xosrov və Şirin\"", "\"Leyli və Məcnun\"", "\"İsgəndərnamə\"", "\"Sirlər xəzinəsi\"", 'D',
            "\"Sirlər xəzinəsi\" (\"Məxzənül-əsrar\") Nizaminin \"Xəmsə\"sinin ilk poemasıdır.");

        yield return Q(now, Difficulty.Hard, "Klassik poeziya",
            "Xaqani Şirvaninin \"Töhfətül-İraqeyn\" əsəri hansı janrdadır?",
            "Məsnəvi", "Qəsidə", "Qəzəl", "Rübai", 'A',
            "\"Töhfətül-İraqeyn\" Xaqaninin səyahət təəssüratları əsasında yazdığı məsnəvidir.");
    }

    private static Question Q(
        DateTime createdAt,
        Difficulty difficulty,
        string category,
        string text,
        string a, string b, string c, string d,
        char correct,
        string? explanation) => new()
    {
        Text = text,
        OptionA = a,
        OptionB = b,
        OptionC = c,
        OptionD = d,
        CorrectOption = correct,
        Difficulty = difficulty,
        Category = category,
        Explanation = explanation,
        CreatedAt = createdAt
    };
}
