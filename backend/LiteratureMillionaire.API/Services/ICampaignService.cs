using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface ICampaignService
{
    /// <summary>The playable campaign of the default quiz mode ("Bilik Dünyası"). Kept for clients that know a single campaign.</summary>
    /// <exception cref="CampaignException">404 when none matches; 500 when the mode has more than one.</exception>
    Task<CurrentCampaignDto> GetCurrentAsync(CancellationToken ct = default);

    /// <summary>Campaigns playable today, at most one per active quiz mode, ordered by the mode's DisplayOrder.</summary>
    /// <exception cref="CampaignException">404 when nothing is playable; 500 when a quiz mode has more than one campaign today.</exception>
    Task<IReadOnlyList<CampaignSummaryDto>> GetAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// The campaign a new quiz starts in: the named campaign when it is playable today, otherwise (no id) the
    /// default mode's campaign.
    /// </summary>
    /// <exception cref="CampaignException">
    /// 404 CAMPAIGN_NOT_FOUND / NO_ACTIVE_CAMPAIGN; 409 CAMPAIGN_NOT_ACTIVE; 500 MULTIPLE_ACTIVE_CAMPAIGNS / CAMPAIGN_BOOK_REQUIRED.
    /// </exception>
    Task<ActiveCampaign> GetPlayableAsync(int? campaignId, CancellationToken ct = default);
}
