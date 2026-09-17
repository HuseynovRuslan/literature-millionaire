namespace LiteratureMillionaire.API.Services;

/// <summary>
/// The one "today" campaigns are judged by. Players and the admin panel must agree on it: a campaign the panel
/// calls running has to be the one players can start.
/// </summary>
public static class CampaignCalendar
{
    /// <summary>Calendar date of the machine the API runs on, not UTC.</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(DateTime.Now);
}
