using System.Globalization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Results and participants in the admin panel (docs/admin-panel-plan.md, phase 3): who played a campaign and how
/// they did, the Excel list prizes are handed out from, and the two corrections that remove data - giving someone
/// their attempt back, and removing a participant (a test entry) entirely.
///
/// Both corrections need a reason and are recorded in the audit trail with who was affected and what their result
/// was, because they change who wins. Neither is allowed while the attempt may still be in play: the game keeps a
/// session for <see cref="GameService.SessionLifetime"/> and would fail to store the result of a deleted attempt.
/// </summary>
public interface IAdminResultsService
{
    /// <returns>Null when there is no such campaign.</returns>
    Task<AdminCampaignResultsDto?> GetAsync(int campaignId, string? search, CancellationToken ct = default);

    /// <returns>Null when there is no such campaign.</returns>
    Task<(byte[] Content, string FileName)?> ExportAsync(int campaignId, AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminResultsException"/>
    Task ResetAttemptAsync(int attemptId, string? reason, AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminResultsException"/>
    Task RemoveParticipantAsync(int participantId, string? reason, AdminActor actor, CancellationToken ct = default);
}

public sealed class AdminResultsException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
}

public sealed class AdminResultsService : IAdminResultsService
{
    public const int ReasonMinLength = 3;
    public const int ReasonMaxLength = 300;

    /// <summary>Azerbaijan keeps UTC+4 all year; the export shows the times people actually saw.</summary>
    private static readonly TimeSpan BakuOffset = TimeSpan.FromHours(4);

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;

    public AdminResultsService(ApplicationDbContext db, IAdminAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AdminCampaignResultsDto?> GetAsync(int campaignId, string? search, CancellationToken ct = default)
    {
        var loaded = await LoadAsync(campaignId, ct);
        if (loaded is null)
        {
            return null;
        }

        var (campaign, rows) = loaded.Value;
        var matching = rows.Where(r => Matches(r, search)).Select(ToDto).ToList();
        return new AdminCampaignResultsDto(
            campaign,
            rows.Count,
            rows.Count(r => r.Attempt.CompletedAtUtc != null),
            rows.Count(r => r.Attempt.Passed == true),
            rows.Count(r => r.Attempt.CompletedAtUtc == null),
            matching);
    }

    public async Task<(byte[] Content, string FileName)?> ExportAsync(int campaignId, AdminActor actor, CancellationToken ct = default)
    {
        var loaded = await LoadAsync(campaignId, ct);
        if (loaded is null)
        {
            return null;
        }

        var (campaign, rows) = loaded.Value;
        var columns = new XlsxWriter.Column[]
        {
            new("Yer", 6), new("Ad Soyad", 28), new("Telefon", 16), new("Düzgün cavab", 13), new("Sual", 6),
            new("Xal", 6), new("Maks. xal", 9), new("Müddət (saniyə)", 15), new("Keçdi", 7),
            new("Başladı (Bakı)", 17), new("Bitirdi (Bakı)", 17), new("Vəziyyət", 11),
        };
        var content = XlsxWriter.Write("Nəticələr", columns, rows.Select(r => (IReadOnlyList<object?>)
        [
            r.Rank,
            r.FullName,
            r.PhoneNumber,
            r.Attempt.CorrectAnswers,
            r.Attempt.TotalQuestions,
            r.Attempt.PointsEarned,
            r.Attempt.MaxPoints,
            Duration(r.Attempt) is { } seconds ? Math.Round(seconds, 1) : null,
            r.Attempt.Passed is { } passed ? (passed ? "Bəli" : "Xeyr") : null,
            Baku(r.Attempt.StartedAtUtc),
            r.Attempt.CompletedAtUtc is { } done ? Baku(done) : null,
            r.Attempt.CompletedAtUtc is null ? "Bitməyib" : "Bitib",
        ]));

        // The export is the one place full phone numbers leave the panel, so it is recorded like a change.
        await _audit.RecordAsync(actor, "results-exported", "campaign", campaignId.ToString(CultureInfo.InvariantCulture),
            $"{campaign.QuizModeTitle} · {rows.Count} cəhd", ct);

        return (content, $"neticeler-kampaniya-{campaignId}-{campaign.EndDate:yyyy-MM-dd}.xlsx");
    }

    public async Task ResetAttemptAsync(int attemptId, string? reason, AdminActor actor, CancellationToken ct = default)
    {
        var why = RequireReason(reason);
        var attempt = await _db.QuizAttempts.Include(a => a.Participant).FirstOrDefaultAsync(a => a.Id == attemptId, ct)
            ?? throw new AdminResultsException(StatusCodes.Status404NotFound, "ATTEMPT_NOT_FOUND", "Belə cəhd yoxdur.");
        EnsureNotInPlay(attempt);

        // One transaction with its audit entry: a deletion never exists without the record of it.
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.QuizAttempts.Remove(attempt);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "attempt-reset", "campaign", attempt.CampaignId.ToString(CultureInfo.InvariantCulture),
            $"{Who(attempt.Participant)} · {Outcome(attempt)} · səbəb: {why}", ct);
        await transaction.CommitAsync(ct);
    }

    public async Task RemoveParticipantAsync(int participantId, string? reason, AdminActor actor, CancellationToken ct = default)
    {
        var why = RequireReason(reason);
        var participant = await _db.Participants.Include(p => p.Attempts).FirstOrDefaultAsync(p => p.Id == participantId, ct)
            ?? throw new AdminResultsException(StatusCodes.Status404NotFound, "PARTICIPANT_NOT_FOUND", "Belə iştirakçı yoxdur.");
        foreach (var attempt in participant.Attempts)
        {
            EnsureNotInPlay(attempt);
        }

        var count = participant.Attempts.Count;
        var summary = string.Join(", ", participant.Attempts.OrderBy(a => a.CampaignId)
            .Select(a => $"kampaniya #{a.CampaignId}: {Outcome(a)}"));
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.QuizAttempts.RemoveRange(participant.Attempts);
        _db.Participants.Remove(participant);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "participant-removed", "participant", participantId.ToString(CultureInfo.InvariantCulture),
            $"{Who(participant)} · {count} cəhd{(summary.Length > 0 ? $" ({summary})" : string.Empty)} · səbəb: {why}", ct);
        await transaction.CommitAsync(ct);
    }

    // --- rules ------------------------------------------------------------------------------------------------

    private static string RequireReason(string? reason)
    {
        var why = reason?.Trim() ?? string.Empty;
        if (why.Length < ReasonMinLength)
        {
            throw new AdminResultsException(StatusCodes.Status400BadRequest, "REASON_REQUIRED", "Səbəbi yazın (məsələn: \"test girişi\").");
        }
        return why.Length <= ReasonMaxLength ? why : why[..ReasonMaxLength];
    }

    private static void EnsureNotInPlay(QuizAttempt attempt)
    {
        var sinceStart = DateTime.UtcNow - attempt.StartedAtUtc;
        if (attempt.CompletedAtUtc is null && sinceStart < GameService.SessionLifetime)
        {
            var wait = (int)Math.Ceiling((GameService.SessionLifetime - sinceStart).TotalMinutes);
            throw new AdminResultsException(StatusCodes.Status409Conflict, "ATTEMPT_IN_PLAY",
                $"Bu cəhd hələ bitməyib və iştirakçı indi oynaya bilər. {wait} dəqiqə sonra yenidən cəhd edin.");
        }
    }

    // --- data -------------------------------------------------------------------------------------------------

    private sealed record Row(QuizAttempt Attempt, string FullName, string PhoneNumber, int? Rank, int ParticipantAttempts);

    private async Task<(AdminResultsCampaignDto Campaign, List<Row> Rows)?> LoadAsync(int campaignId, CancellationToken ct)
    {
        var campaign = await _db.MonthlyCampaigns.AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => new AdminResultsCampaignDto(c.Id, c.QuizMode.Title, c.BookId != null ? c.Book!.Title : null,
                c.StartDate, c.EndDate, c.PassingScore, c.RewardTitle))
            .FirstOrDefaultAsync(ct);
        if (campaign is null)
        {
            return null;
        }

        var attempts = await _db.QuizAttempts.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .Select(a => new { Attempt = a, a.Participant.FullName, a.Participant.NormalizedPhoneNumber, Total = a.Participant.Attempts.Count })
            .ToListAsync(ct);

        // The public leaderboard's own ranking, so the panel and the screen players see never disagree.
        var ranks = LeaderboardRanking.Rank(attempts
                .Where(x => x.Attempt is { CompletedAtUtc: not null, CorrectAnswers: not null, PointsEarned: not null, Passed: not null })
                .Select(x => new LeaderboardCandidate(x.Attempt.Id, x.Attempt.ParticipantId, x.FullName, x.Attempt.StartedAtUtc,
                    x.Attempt.CompletedAtUtc!.Value, x.Attempt.CorrectAnswers!.Value, x.Attempt.PointsEarned!.Value,
                    x.Attempt.MaxPoints, x.Attempt.TotalQuestions)))
            .ToDictionary(r => r.Candidate.AttemptId, r => r.Rank);

        var rows = attempts
            .Select(x => new Row(x.Attempt, x.FullName, x.NormalizedPhoneNumber, ranks.TryGetValue(x.Attempt.Id, out var rank) ? rank : null, x.Total))
            .OrderBy(r => r.Rank ?? int.MaxValue)
            .ThenByDescending(r => r.Attempt.StartedAtUtc)
            .ToList();
        return (campaign, rows);
    }

    /// <summary>Name, ignoring case and Azerbaijani letters typed without their marks; or three or more digits of the phone.</summary>
    private static bool Matches(Row row, string? search)
    {
        var query = search?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return true;
        }

        if (CultureInfo.InvariantCulture.CompareInfo.IndexOf(row.FullName, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
        {
            return true;
        }

        var digits = new string(query.Where(char.IsAsciiDigit).ToArray());
        return digits.Length >= 3 && row.PhoneNumber.Contains(digits, StringComparison.Ordinal);
    }

    private static AdminAttemptRowDto ToDto(Row r) => new(
        r.Attempt.Id,
        r.Attempt.ParticipantId,
        r.Rank,
        r.FullName,
        PhoneNumber.Mask(r.PhoneNumber),
        r.Attempt.StartedAtUtc,
        r.Attempt.CompletedAtUtc,
        r.Attempt.CorrectAnswers,
        r.Attempt.TotalQuestions,
        r.Attempt.PointsEarned,
        r.Attempt.MaxPoints,
        Duration(r.Attempt),
        r.Attempt.Passed,
        r.ParticipantAttempts);

    private static double? Duration(QuizAttempt attempt) =>
        attempt.CompletedAtUtc is { } done ? Math.Max(0, (done - attempt.StartedAtUtc).TotalSeconds) : null;

    private static string Baku(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(BakuOffset).ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Who(Participant participant) => $"{participant.FullName} ({PhoneNumber.Mask(participant.NormalizedPhoneNumber)})";

    private static string Outcome(QuizAttempt attempt) =>
        attempt.CompletedAtUtc is null
            ? "bitməmiş"
            : $"{attempt.CorrectAnswers}/{attempt.TotalQuestions} düzgün, {attempt.PointsEarned} xal{(attempt.Passed == true ? ", keçdi" : string.Empty)}";
}
