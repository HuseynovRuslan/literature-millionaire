using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface ICampaignService
{
    /// <summary>The single campaign running on the backend's current calendar date.</summary>
    /// <exception cref="CampaignException">404 when none matches; 500 when more than one matches.</exception>
    Task<CurrentCampaignDto> GetCurrentAsync(CancellationToken ct = default);
}
