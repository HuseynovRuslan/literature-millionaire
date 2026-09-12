namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Campaign lookup problem that maps to an HTTP problem response with a stable
/// machine-readable <see cref="Code"/>. Thrown by <see cref="CampaignService"/>.
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
            "There is no enabled campaign with an active book covering today's date.");

    public static CampaignException MultipleActiveCampaigns(int count) =>
        new(StatusCodes.Status500InternalServerError, "Campaign configuration error", "MULTIPLE_ACTIVE_CAMPAIGNS",
            $"{count} campaigns match today's date. Exactly one must be current; fix the campaign dates or disable the extras.");
}
