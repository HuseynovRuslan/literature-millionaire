using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface ILeaderboardService
{
    /// <summary>Returns the first <paramref name="limit"/> participants, based on each participant's best completed attempt.</summary>
    /// <exception cref="CampaignException">404 when the campaign does not exist.</exception>
    Task<LeaderboardDto> GetAsync(int campaignId, int limit, CancellationToken ct = default);

    /// <summary>Returns a participant's rank based on their best completed attempt, or null when they have no completed attempt.</summary>
    Task<int?> GetPositionAsync(int campaignId, int participantId, CancellationToken ct = default);
}
