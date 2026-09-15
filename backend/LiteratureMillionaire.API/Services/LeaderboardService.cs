using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

public class LeaderboardService : ILeaderboardService
{
    private readonly ApplicationDbContext _db;

    public LeaderboardService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LeaderboardDto> GetAsync(int campaignId, int limit, CancellationToken ct = default)
    {
        if (limit is < 1 or > 10)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be between 1 and 10.");
        }

        if (!await _db.MonthlyCampaigns.AsNoTracking().AnyAsync(campaign => campaign.Id == campaignId, ct))
        {
            throw CampaignException.CampaignNotFound(campaignId);
        }

        var ranked = LeaderboardRanking.Rank(await LoadCompletedAttemptsAsync(campaignId, ct));
        var entries = ranked
            .Take(limit)
            .Select(item =>
            {
                var attempt = item.Candidate;
                return new LeaderboardEntryDto(
                    Rank: item.Rank,
                    DisplayName: LeaderboardNameMasker.Mask(attempt.FullName),
                    PointsEarned: attempt.PointsEarned,
                    MaxPoints: attempt.MaxPoints,
                    CorrectAnswers: attempt.CorrectAnswers,
                    TotalQuestions: attempt.TotalQuestions,
                    DurationSeconds: attempt.DurationTicks / (double)TimeSpan.TicksPerSecond,
                    CompletedAtUtc: attempt.CompletedAtUtc);
            })
            .ToArray();

        return new LeaderboardDto(campaignId, DateTime.UtcNow, entries);
    }

    public async Task<int?> GetPositionAsync(int campaignId, int participantId, CancellationToken ct = default) =>
        LeaderboardRanking.Rank(await LoadCompletedAttemptsAsync(campaignId, ct))
            .FirstOrDefault(item => item.Candidate.ParticipantId == participantId)
            ?.Rank;

    private Task<List<LeaderboardCandidate>> LoadCompletedAttemptsAsync(int campaignId, CancellationToken ct) =>
        _db.QuizAttempts
            .AsNoTracking()
            .Where(attempt => attempt.CampaignId == campaignId
                              && attempt.CompletedAtUtc.HasValue
                              && attempt.CorrectAnswers.HasValue
                              && attempt.PointsEarned.HasValue
                              && attempt.Passed.HasValue)
            // Deliberately project FullName but never either phone field. IDs remain internal
            // inputs for deduplication/tie-breaking and are never copied to a public DTO.
            .Select(attempt => new LeaderboardCandidate(
                attempt.Id,
                attempt.ParticipantId,
                attempt.Participant.FullName,
                attempt.StartedAtUtc,
                attempt.CompletedAtUtc!.Value,
                attempt.CorrectAnswers!.Value,
                attempt.PointsEarned!.Value,
                attempt.MaxPoints,
                attempt.TotalQuestions))
            .ToListAsync(ct);
}
