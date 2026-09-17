using System.Globalization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Campaigns as administrators manage them (docs/admin-panel-plan.md, phase 2): list, create, edit, switch on
/// and off. Nothing is ever deleted - a campaign carries attempts and results, so it is switched off instead.
///
/// A campaign is refused when it could not be played the way it is configured: an enabled campaign must be able
/// to build a round from its bank, name a book when its mode is played per book, and not overlap another enabled
/// campaign of the same mode (players could not be told which one they are in). A switched-off campaign may be
/// saved incomplete, as a draft; what is still missing is listed with it.
/// </summary>
public interface IAdminCampaignService
{
    Task<IReadOnlyList<AdminCampaignDto>> ListAsync(CancellationToken ct = default);
    Task<AdminCampaignOptionsDto> OptionsAsync(CancellationToken ct = default);

    /// <exception cref="AdminCampaignValidationException">When the campaign cannot be saved as sent.</exception>
    Task<AdminCampaignDto> CreateAsync(AdminCampaignInput input, AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminCampaignValidationException">When the campaign cannot be saved as sent.</exception>
    /// <returns>Null when there is no such campaign.</returns>
    Task<AdminCampaignDto?> UpdateAsync(int id, AdminCampaignInput input, AdminActor actor, CancellationToken ct = default);
}

/// <summary>Field → messages, in Azerbaijani, ready for the form. Keys are the input's camelCase property names.</summary>
public sealed class AdminCampaignValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The campaign cannot be saved.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AdminCampaignService : IAdminCampaignService
{
    /// <summary>Longest campaign accepted. Catches a mistyped year (2062) rather than limiting anyone.</summary>
    public const int MaxCampaignDays = 366;

    private static readonly CultureInfo Az = CultureInfo.GetCultureInfo("az-Latn-AZ");

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;

    public AdminCampaignService(ApplicationDbContext db, IAdminAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AdminCampaignDto>> ListAsync(CancellationToken ct = default)
    {
        var context = await LoadContextAsync(ct);
        return context.Campaigns
            .OrderBy(c => StatusRank(Status(c, context.Today)))
            .ThenByDescending(c => c.StartDate)
            .ThenBy(c => context.Modes.TryGetValue(c.QuizModeId, out var m) ? m.DisplayOrder : int.MaxValue)
            .Select(c => ToDto(c, context))
            .ToList();
    }

    public async Task<AdminCampaignOptionsDto> OptionsAsync(CancellationToken ct = default)
    {
        var modes = await _db.QuizModes.AsNoTracking().OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToListAsync(ct);
        var books = await _db.Books.AsNoTracking().OrderBy(b => b.Title).ToListAsync(ct);
        var pools = await LoadPoolsAsync(ct);

        return new AdminCampaignOptionsDto(
            CampaignCalendar.Today(),
            modes.Select(m => new AdminQuizModeOptionDto(m.Id, m.Title, m.IsActive, QuizModeSlugs.RequiresBook(m.Slug))).ToList(),
            books.Select(b => new AdminBookOptionDto(b.Id, b.Title, b.Author, b.IsActive)).ToList(),
            pools.Select(p => new AdminQuestionPoolDto(p.QuizModeId, p.BookId, p.Easy, p.Medium, p.Hard, p.Images)).ToList(),
            QuizRules.EasyPerQuiz,
            QuizRules.MediumPerQuiz,
            QuizRules.HardPerQuiz);
    }

    public async Task<AdminCampaignDto> CreateAsync(AdminCampaignInput input, AdminActor actor, CancellationToken ct = default)
    {
        var context = await LoadContextAsync(ct);
        var campaign = new MonthlyCampaign();
        var errors = Validate(input, existing: null, context);
        if (input.EndDate is { } end && end < context.Today)
        {
            Add(errors, "endDate", "Bitmiş tarixlərlə kampaniya yaratmaq olmaz.");
        }
        ThrowIfAny(errors);

        Apply(campaign, input);
        _db.MonthlyCampaigns.Add(campaign);
        await _db.SaveChangesAsync(ct);

        await _audit.RecordAsync(actor, "campaign-created", "campaign", campaign.Id.ToString(CultureInfo.InvariantCulture),
            Describe(campaign, context), ct);

        context.Campaigns.Add(campaign);
        return ToDto(campaign, context);
    }

    public async Task<AdminCampaignDto?> UpdateAsync(int id, AdminCampaignInput input, AdminActor actor, CancellationToken ct = default)
    {
        var campaign = await _db.MonthlyCampaigns.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (campaign is null)
        {
            return null;
        }

        var context = await LoadContextAsync(ct);
        var errors = Validate(input, campaign, context);
        ThrowIfAny(errors);

        var before = Snapshot(campaign);
        Apply(campaign, input);
        var changes = Changes(before, Snapshot(campaign), context);
        if (changes.Count == 0)
        {
            return ToDto(campaign, context);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "campaign-updated", "campaign", campaign.Id.ToString(CultureInfo.InvariantCulture),
            string.Join("; ", changes), ct);

        var index = context.Campaigns.FindIndex(c => c.Id == campaign.Id);
        context.Campaigns[index] = campaign;
        return ToDto(campaign, context);
    }

    // --- rules ------------------------------------------------------------------------------------------------

    private static Dictionary<string, List<string>> Validate(AdminCampaignInput input, MonthlyCampaign? existing, Context context)
    {
        var errors = new Dictionary<string, List<string>>();

        QuizMode? mode = null;
        if (input.QuizModeId is not { } modeId)
        {
            Add(errors, "quizModeId", "Kateqoriya seçin.");
        }
        else if (!context.Modes.TryGetValue(modeId, out mode))
        {
            Add(errors, "quizModeId", "Belə kateqoriya yoxdur.");
        }

        if (input.BookId is { } bookId && !context.Books.ContainsKey(bookId))
        {
            Add(errors, "bookId", "Belə kitab yoxdur.");
        }

        if (input.StartDate is null) Add(errors, "startDate", "Başlama tarixini seçin.");
        if (input.EndDate is null) Add(errors, "endDate", "Bitmə tarixini seçin.");
        if (input.StartDate is { } start && input.EndDate is { } end)
        {
            if (end < start)
            {
                Add(errors, "endDate", "Bitmə tarixi başlama tarixindən əvvəl ola bilməz.");
            }
            else if (end.DayNumber - start.DayNumber + 1 > MaxCampaignDays)
            {
                Add(errors, "endDate", $"Kampaniya ən çox {MaxCampaignDays} gün davam edə bilər. Tarixi yoxlayın.");
            }
        }

        if (input.PassingScore is < 1 or > QuizRules.QuestionsPerQuiz)
        {
            Add(errors, "passingScore", $"Keçid balı 1 ilə {QuizRules.QuestionsPerQuiz} arasında olmalıdır.");
        }

        if (input.ImageQuestionsPerQuiz is < 0 or > QuizRules.QuestionsPerQuiz)
        {
            Add(errors, "imageQuestionsPerQuiz", $"Şəkilli sualların sayı 0 ilə {QuizRules.QuestionsPerQuiz} arasında olmalıdır.");
        }

        var reward = input.RewardTitle?.Trim() ?? string.Empty;
        if (reward.Length == 0)
        {
            Add(errors, "rewardTitle", "Mükafatı yazın.");
        }
        else if (reward.Length > 200)
        {
            Add(errors, "rewardTitle", "Mükafat ən çox 200 simvol ola bilər.");
        }

        // Attempts belong to the round they were played in: moving a campaign that has them to another category
        // or book would put those results under questions nobody answered.
        if (existing is not null && (existing.QuizModeId != input.QuizModeId || existing.BookId != input.BookId)
            && context.Attempts.TryGetValue(existing.Id, out var attempts) && attempts.Started > 0)
        {
            Add(errors, existing.QuizModeId != input.QuizModeId ? "quizModeId" : "bookId",
                $"Bu kampaniyada artıq {attempts.Started} cəhd var: kateqoriyanı və kitabı dəyişmək olmaz. Yeni kampaniya yaradın.");
        }

        if (mode is not null && QuizModeSlugs.RequiresBook(mode.Slug) && input.BookId is null)
        {
            Add(errors, "bookId", $"\"{mode.Title}\" kampaniyası üçün kitab seçin.");
        }

        // What only matters for a campaign players will see.
        if (input.IsEnabled && mode is not null && errors.Count == 0)
        {
            var shortfall = Shortfall(context.PoolFor(mode.Id, input.BookId));
            if (shortfall is not null)
            {
                Add(errors, input.BookId is null ? "quizModeId" : "bookId", $"Sual bankı raundu doldurmur ({shortfall}). Kampaniyanı deaktiv saxlayın və ya başqa bank seçin.");
            }

            var overlap = Overlapping(existing?.Id, mode.Id, input.StartDate!.Value, input.EndDate!.Value, context);
            if (overlap is not null)
            {
                Add(errors, "startDate",
                    $"Bu tarixlərdə \"{mode.Title}\" kateqoriyasının başqa aktiv kampaniyası var (#{overlap.Id}, {Date(overlap.StartDate)} – {Date(overlap.EndDate)}). Tarixləri dəyişin və ya o kampaniyanı deaktiv edin.");
            }
        }

        return errors;
    }

    private static MonthlyCampaign? Overlapping(int? selfId, int modeId, DateOnly start, DateOnly end, Context context) =>
        context.Campaigns
            .Where(c => c.Id != selfId && c.IsEnabled && c.QuizModeId == modeId && c.StartDate <= end && c.EndDate >= start)
            .OrderBy(c => c.Id)
            .FirstOrDefault();

    /// <summary>"asan 2/3, çətin 1/3" for the difficulties the pool cannot fill; null when a round can be built.</summary>
    private static string? Shortfall(Pool pool)
    {
        var missing = new List<string>();
        if (pool.Easy < QuizRules.EasyPerQuiz) missing.Add($"asan {pool.Easy}/{QuizRules.EasyPerQuiz}");
        if (pool.Medium < QuizRules.MediumPerQuiz) missing.Add($"orta {pool.Medium}/{QuizRules.MediumPerQuiz}");
        if (pool.Hard < QuizRules.HardPerQuiz) missing.Add($"çətin {pool.Hard}/{QuizRules.HardPerQuiz}");
        return missing.Count == 0 ? null : string.Join(", ", missing);
    }

    private static IReadOnlyList<string> Issues(MonthlyCampaign campaign, Context context)
    {
        var issues = new List<string>();
        var status = Status(campaign, context.Today);
        if (status == "ended")
        {
            return issues; // history: nothing to fix
        }

        context.Modes.TryGetValue(campaign.QuizModeId, out var mode);
        if (mode is { IsActive: false })
        {
            issues.Add("Kateqoriya deaktivdir: oyunçular bu kampaniyanı görmür.");
        }

        if (campaign.BookId is { } bookId && context.Books.TryGetValue(bookId, out var book) && !book.IsActive)
        {
            issues.Add("Kitab deaktivdir: oyunçular bu kampaniyanı görmür.");
        }

        if (mode is not null && QuizModeSlugs.RequiresBook(mode.Slug) && campaign.BookId is null)
        {
            issues.Add("Kitab seçilməyib.");
        }

        var pool = context.PoolFor(campaign.QuizModeId, campaign.BookId);
        if (Shortfall(pool) is { } shortfall)
        {
            issues.Add($"Sual bankı raundu doldurmur: {shortfall}.");
        }
        else
        {
            var usable = Math.Min(pool.EasyImages, QuizRules.EasyPerQuiz) + Math.Min(pool.MediumImages, QuizRules.MediumPerQuiz)
                         + Math.Min(pool.HardImages, QuizRules.HardPerQuiz);
            if (campaign.ImageQuestionsPerQuiz > usable)
            {
                issues.Add($"Şəkilli sual azdır: raundda {campaign.ImageQuestionsPerQuiz} əvəzinə {usable} şəkilli sual olacaq.");
            }
        }

        if (campaign.IsEnabled && Overlapping(campaign.Id, campaign.QuizModeId, campaign.StartDate, campaign.EndDate, context) is { } other)
        {
            issues.Add($"#{other.Id} kampaniyası ilə tarixlər üst-üstə düşür.");
        }

        return issues;
    }

    private static string Status(MonthlyCampaign campaign, DateOnly today) =>
        campaign.EndDate < today ? "ended"
        : !campaign.IsEnabled ? "disabled"
        : campaign.StartDate > today ? "scheduled"
        : "running";

    private static int StatusRank(string status) => status switch
    {
        "running" => 0,
        "scheduled" => 1,
        "disabled" => 2,
        _ => 3,
    };

    // --- data -------------------------------------------------------------------------------------------------

    private sealed record AttemptCounts(int Started, int Completed);

    private sealed record Pool(int QuizModeId, int? BookId, int Easy, int Medium, int Hard, int EasyImages, int MediumImages, int HardImages)
    {
        public int Images => EasyImages + MediumImages + HardImages;
    }

    private sealed class Context
    {
        public required DateOnly Today { get; init; }
        public required List<MonthlyCampaign> Campaigns { get; init; }
        public required Dictionary<int, QuizMode> Modes { get; init; }
        public required Dictionary<int, Book> Books { get; init; }
        public required List<Pool> Pools { get; init; }
        public required Dictionary<int, AttemptCounts> Attempts { get; init; }

        /// <summary>What a round would draw on - the same filter GameService uses: the mode, and the book when there is one.</summary>
        public Pool PoolFor(int modeId, int? bookId)
        {
            var rows = Pools.Where(p => p.QuizModeId == modeId && (bookId == null || p.BookId == bookId)).ToList();
            return new Pool(modeId, bookId, rows.Sum(p => p.Easy), rows.Sum(p => p.Medium), rows.Sum(p => p.Hard),
                rows.Sum(p => p.EasyImages), rows.Sum(p => p.MediumImages), rows.Sum(p => p.HardImages));
        }
    }

    private async Task<Context> LoadContextAsync(CancellationToken ct)
    {
        var campaigns = await _db.MonthlyCampaigns.AsNoTracking().ToListAsync(ct);
        var modes = await _db.QuizModes.AsNoTracking().ToDictionaryAsync(m => m.Id, ct);
        var books = await _db.Books.AsNoTracking().ToDictionaryAsync(b => b.Id, ct);
        var attempts = await _db.QuizAttempts.AsNoTracking()
            .GroupBy(a => a.CampaignId)
            .Select(g => new { CampaignId = g.Key, Started = g.Count(), Completed = g.Count(a => a.CompletedAtUtc != null) })
            .ToDictionaryAsync(a => a.CampaignId, a => new AttemptCounts(a.Started, a.Completed), ct);

        return new Context
        {
            Today = CampaignCalendar.Today(),
            Campaigns = campaigns,
            Modes = modes,
            Books = books,
            Pools = await LoadPoolsAsync(ct),
            Attempts = attempts,
        };
    }

    private async Task<List<Pool>> LoadPoolsAsync(CancellationToken ct)
    {
        var counts = await _db.Questions.AsNoTracking()
            .Where(q => q.QuizModeId != null)
            .GroupBy(q => new { q.QuizModeId, q.BookId, q.Difficulty, HasImage = q.ImageUrl != null })
            .Select(g => new { g.Key.QuizModeId, g.Key.BookId, g.Key.Difficulty, g.Key.HasImage, Count = g.Count() })
            .ToListAsync(ct);

        return counts
            .GroupBy(c => new { QuizModeId = c.QuizModeId!.Value, c.BookId })
            .Select(g =>
            {
                int Count(Difficulty d, bool? image) => g.Where(c => c.Difficulty == d && (image == null || c.HasImage == image)).Sum(c => c.Count);
                return new Pool(g.Key.QuizModeId, g.Key.BookId,
                    Count(Difficulty.Easy, null), Count(Difficulty.Medium, null), Count(Difficulty.Hard, null),
                    Count(Difficulty.Easy, true), Count(Difficulty.Medium, true), Count(Difficulty.Hard, true));
            })
            .OrderBy(p => p.QuizModeId)
            .ThenBy(p => p.BookId)
            .ToList();
    }

    // --- mapping ----------------------------------------------------------------------------------------------

    private static void Apply(MonthlyCampaign campaign, AdminCampaignInput input)
    {
        campaign.QuizModeId = input.QuizModeId!.Value;
        campaign.BookId = input.BookId;
        campaign.StartDate = input.StartDate!.Value;
        campaign.EndDate = input.EndDate!.Value;
        campaign.PassingScore = input.PassingScore;
        campaign.RewardTitle = input.RewardTitle!.Trim();
        campaign.ImageQuestionsPerQuiz = input.ImageQuestionsPerQuiz;
        campaign.IsEnabled = input.IsEnabled;
    }

    private static AdminCampaignDto ToDto(MonthlyCampaign c, Context context)
    {
        context.Attempts.TryGetValue(c.Id, out var attempts);
        return new AdminCampaignDto(
            c.Id,
            c.QuizModeId,
            context.Modes.TryGetValue(c.QuizModeId, out var mode) ? mode.Title : string.Empty,
            c.BookId,
            c.BookId is { } bookId && context.Books.TryGetValue(bookId, out var book) ? book.Title : null,
            c.StartDate,
            c.EndDate,
            c.PassingScore,
            c.RewardTitle,
            c.ImageQuestionsPerQuiz,
            c.IsEnabled,
            Status(c, context.Today),
            attempts?.Started ?? 0,
            attempts?.Completed ?? 0,
            Issues(c, context));
    }

    private sealed record CampaignSnapshot(int QuizModeId, int? BookId, DateOnly StartDate, DateOnly EndDate, int PassingScore,
        string RewardTitle, int ImageQuestionsPerQuiz, bool IsEnabled);

    private static CampaignSnapshot Snapshot(MonthlyCampaign c) =>
        new(c.QuizModeId, c.BookId, c.StartDate, c.EndDate, c.PassingScore, c.RewardTitle, c.ImageQuestionsPerQuiz, c.IsEnabled);

    /// <summary>What an edit changed, in words, for the audit trail: "Bitmə tarixi: 30.09.2026 → 31.10.2026".</summary>
    private static List<string> Changes(CampaignSnapshot before, CampaignSnapshot after, Context context)
    {
        var changes = new List<string>();
        void Compare<T>(string label, T old, T now, Func<T, string> show)
        {
            if (!EqualityComparer<T>.Default.Equals(old, now)) changes.Add($"{label}: {show(old)} → {show(now)}");
        }

        Compare("Aktiv", before.IsEnabled, after.IsEnabled, YesNo);
        Compare("Kateqoriya", before.QuizModeId, after.QuizModeId, id => ModeTitle(id, context));
        Compare("Kitab", before.BookId, after.BookId, id => BookTitle(id, context));
        Compare("Başlama tarixi", before.StartDate, after.StartDate, Date);
        Compare("Bitmə tarixi", before.EndDate, after.EndDate, Date);
        Compare("Keçid balı", before.PassingScore, after.PassingScore, v => v.ToString(CultureInfo.InvariantCulture));
        Compare("Mükafat", before.RewardTitle, after.RewardTitle, v => $"\"{v}\"");
        Compare("Şəkilli sual", before.ImageQuestionsPerQuiz, after.ImageQuestionsPerQuiz, v => v.ToString(CultureInfo.InvariantCulture));
        return changes;
    }

    private static string Describe(MonthlyCampaign c, Context context) =>
        $"{ModeTitle(c.QuizModeId, context)}{(c.BookId is null ? string.Empty : $" · {BookTitle(c.BookId, context)}")}"
        + $" · {Date(c.StartDate)} – {Date(c.EndDate)} · keçid balı {c.PassingScore} · {(c.IsEnabled ? "aktiv" : "deaktiv")}";

    private static string ModeTitle(int id, Context context) => context.Modes.TryGetValue(id, out var m) ? m.Title : $"#{id}";

    private static string BookTitle(int? id, Context context) =>
        id is null ? "yoxdur" : context.Books.TryGetValue(id.Value, out var b) ? b.Title : $"#{id}";

    private static string Date(DateOnly date) => date.ToString("dd.MM.yyyy", Az);

    private static string YesNo(bool value) => value ? "bəli" : "xeyr";

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.Add(message);
    }

    private static void ThrowIfAny(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new AdminCampaignValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
