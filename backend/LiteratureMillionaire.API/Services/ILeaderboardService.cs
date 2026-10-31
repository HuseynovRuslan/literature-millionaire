using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface ILeaderboardService
{
    /// <summary>
    /// Every participant with a completed attempt, ranked by each one's best attempt; <paramref name="limit"/> keeps
    /// only the first places (the result screen's short preview), null keeps everyone.
    /// </summary>
    /// <exception cref="CampaignException">404 when the campaign does not exist.</exception>
    Task<LeaderboardDto> GetAsync(int campaignId, int? limit = null, CancellationToken ct = default);

    /// <summary>Returns a participant's rank based on their best completed attempt, or null when they have no completed attempt.</summary>
    Task<int?> GetPositionAsync(int campaignId, int participantId, CancellationToken ct = default);
}
