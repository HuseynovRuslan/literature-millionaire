namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Campaign lookup problem that maps to an HTTP problem response with a stable
/// machine-readable <see cref="Code"/>. Thrown by <see cref="CampaignService"/>.
/// Messages carry campaign ids and counts only, never participant data.
/// </summary>
public class CampaignException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public string Code { get; }

    public CampaignException(int statusCode, string title, string code, string detail) : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Code = code;
    }

    public static CampaignException NoActiveCampaign() =>
        new(StatusCodes.Status404NotFound, "No active campaign", "NO_ACTIVE_CAMPAIGN",
            "There is no enabled campaign of an active quiz mode covering today's date.");

    public static CampaignException CampaignNotFound(int campaignId) =>
        new(StatusCodes.Status404NotFound, "Campaign not found", "CAMPAIGN_NOT_FOUND",
            $"Campaign {campaignId} does not exist.");

    public static CampaignException CampaignNotActive(int campaignId) =>
        new(StatusCodes.Status409Conflict, "Campaign not active", "CAMPAIGN_NOT_ACTIVE",
            $"Campaign {campaignId} cannot be played today: it is disabled, outside its dates, or its quiz mode or book is inactive.");

    public static CampaignException MultipleActiveCampaigns(int count) =>
        new(StatusCodes.Status500InternalServerError, "Campaign configuration error", "MULTIPLE_ACTIVE_CAMPAIGNS",
            $"{count} enabled campaigns of the same quiz mode cover today's date. At most one per quiz mode may be current; fix the campaign dates or disable the extras.");

    public static CampaignException CampaignBookRequired(int campaignId) =>
        new(StatusCodes.Status500InternalServerError, "Campaign configuration error", "CAMPAIGN_BOOK_REQUIRED",
            $"Campaign {campaignId} belongs to a quiz mode that is played per book, but it has no book.");
}
