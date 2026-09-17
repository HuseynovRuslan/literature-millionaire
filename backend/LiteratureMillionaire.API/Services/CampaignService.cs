using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Which campaigns can be played today. A campaign is playable when it is enabled, its quiz mode is active, its
/// book (if any) is active and today's date falls within its range. Each quiz mode may have at most one playable
/// campaign per date; different modes may overlap freely.
/// </summary>
public class CampaignService : ICampaignService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(ApplicationDbContext db, ILogger<CampaignService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>Flat projection of a campaign with its mode and optional book, shared by every lookup.</summary>
    private sealed record CampaignRow(
        int CampaignId,
        DateOnly StartDate,
        DateOnly EndDate,
        int PassingScore,
        string RewardTitle,
        int ImageQuestionsPerQuiz,
        bool IsEnabled,
        int QuizModeId,
        string QuizModeSlug,
        string QuizModeTitle,
        string QuizModeDescription,
        string QuizModeIconKey,
        int QuizModeDisplayOrder,
        bool QuizModeIsActive,
        int? BookId,
        string? BookTitle,
        string? BookAuthor,
        string? BookDescription,
        string? BookCoverImageUrl,
        bool? BookIsActive);

    public async Task<CurrentCampaignDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var row = await GetDefaultModeRowAsync(ct);
        return new CurrentCampaignDto(
            row.CampaignId,
            row.StartDate,
            row.EndDate,
            row.PassingScore,
            row.RewardTitle,
            QuizRules.QuestionsPerQuiz,
            ToBook(row));
    }

    public async Task<IReadOnlyList<CampaignSummaryDto>> GetAvailableAsync(CancellationToken ct = default)
    {
        var today = Today();
        var rows = await Rows(PlayableOn(today)
                .OrderBy(c => c.QuizMode.DisplayOrder)
                .ThenBy(c => c.QuizModeId)
                .ThenBy(c => c.Id))
            .ToListAsync(ct);

        EnsureOnePerMode(rows, today);

        var playable = rows.Where(HasRequiredBook).ToList();
        if (playable.Count == 0)
        {
            throw CampaignException.NoActiveCampaign();
        }

        return playable.Select(ToSummary).ToArray();
    }

    public async Task<ActiveCampaign> GetPlayableAsync(int? campaignId, CancellationToken ct = default)
    {
        if (campaignId is null)
        {
            return ToActive(await GetDefaultModeRowAsync(ct));
        }

        var id = campaignId.Value;
        var today = Today();
        var row = await Rows(_db.MonthlyCampaigns.Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw CampaignException.CampaignNotFound(id);

        var playable = row.IsEnabled
                       && row.QuizModeIsActive
                       && (row.BookId is null || row.BookIsActive == true)
                       && row.StartDate <= today
                       && row.EndDate >= today;
        if (!playable)
        {
            throw CampaignException.CampaignNotActive(id);
        }

        var modeId = row.QuizModeId;
        var sameMode = await Rows(PlayableOn(today).Where(c => c.QuizModeId == modeId).OrderBy(c => c.Id)).Take(2).ToListAsync(ct);
        EnsureOnePerMode(sameMode, today);

        if (!HasRequiredBook(row))
        {
            throw CampaignException.CampaignBookRequired(id);
        }

        return ToActive(row);
    }

    // --- queries ----------------------------------------------------------------

    private static DateOnly Today() => CampaignCalendar.Today();

    private IQueryable<MonthlyCampaign> PlayableOn(DateOnly today) =>
        _db.MonthlyCampaigns.Where(c => c.IsEnabled
                                        && c.QuizMode.IsActive
                                        && (c.BookId == null || c.Book!.IsActive)
                                        && c.StartDate <= today
                                        && c.EndDate >= today);

    private static IQueryable<CampaignRow> Rows(IQueryable<MonthlyCampaign> source) =>
        source.AsNoTracking().Select(c => new CampaignRow(
            c.Id,
            c.StartDate,
            c.EndDate,
            c.PassingScore,
            c.RewardTitle,
            c.ImageQuestionsPerQuiz,
            c.IsEnabled,
            c.QuizModeId,
            c.QuizMode.Slug,
            c.QuizMode.Title,
            c.QuizMode.Description,
            c.QuizMode.IconKey,
            c.QuizMode.DisplayOrder,
            c.QuizMode.IsActive,
            c.BookId,
            c.BookId != null ? c.Book!.Title : null,
            c.BookId != null ? c.Book!.Author : null,
            c.BookId != null ? c.Book!.Description : null,
            c.BookId != null ? c.Book!.CoverImageUrl : null,
            c.BookId != null ? (bool?)c.Book!.IsActive : null));

    /// <summary>The single playable campaign of the default mode. Take(2): enough to tell "one" from "more than one".</summary>
    private async Task<CampaignRow> GetDefaultModeRowAsync(CancellationToken ct)
    {
        var today = Today();
        var rows = await Rows(PlayableOn(today).Where(c => c.QuizMode.Slug == QuizModeSlugs.Default).OrderBy(c => c.Id))
            .Take(2)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            throw CampaignException.NoActiveCampaign();
        }

        EnsureOnePerMode(rows, today);
        return rows[0];
    }

    // --- rules ------------------------------------------------------------------

    /// <summary>At most one playable campaign per quiz mode on a date. Logs ids and the mode slug only.</summary>
    private void EnsureOnePerMode(IReadOnlyCollection<CampaignRow> rows, DateOnly today)
    {
        var conflict = rows.GroupBy(r => r.QuizModeId).FirstOrDefault(g => g.Count() > 1);
        if (conflict is null)
        {
            return;
        }

        var ids = conflict.Select(r => r.CampaignId).ToArray();
        _logger.LogError(
            "Campaign configuration error: quiz mode {QuizModeSlug} has {Count} enabled campaigns covering {Today} (ids {CampaignIds}). At most one per quiz mode may be current.",
            conflict.First().QuizModeSlug, ids.Length, today, string.Join(", ", ids));
        throw CampaignException.MultipleActiveCampaigns(ids.Length);
    }

    /// <summary>"Ayın Kitabı" campaigns are played per book; one without a book is misconfigured and never offered.</summary>
    private bool HasRequiredBook(CampaignRow row)
    {
        if (row.BookId is not null || !QuizModeSlugs.RequiresBook(row.QuizModeSlug))
        {
            return true;
        }

        _logger.LogError(
            "Campaign configuration error: campaign {CampaignId} of quiz mode {QuizModeSlug} has no book.",
            row.CampaignId, row.QuizModeSlug);
        return false;
    }

    // --- mapping ----------------------------------------------------------------

    private static CampaignSummaryDto ToSummary(CampaignRow row) => new(
        row.CampaignId,
        row.StartDate,
        row.EndDate,
        row.PassingScore,
        row.RewardTitle,
        QuizRules.QuestionsPerQuiz,
        row.ImageQuestionsPerQuiz,
        new QuizModeDto(row.QuizModeId, row.QuizModeSlug, row.QuizModeTitle, row.QuizModeDescription, row.QuizModeIconKey, row.QuizModeDisplayOrder),
        ToBook(row));

    private static CampaignBookDto? ToBook(CampaignRow row) =>
        row.BookId is int bookId
            ? new CampaignBookDto(bookId, row.BookTitle ?? string.Empty, row.BookAuthor ?? string.Empty, row.BookDescription ?? string.Empty, row.BookCoverImageUrl ?? string.Empty)
            : null;

    private static ActiveCampaign ToActive(CampaignRow row) => new(
        row.CampaignId,
        row.PassingScore,
        row.RewardTitle,
        row.ImageQuestionsPerQuiz,
        row.QuizModeId,
        row.QuizModeSlug,
        row.QuizModeTitle,
        row.BookId);
}
