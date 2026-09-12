namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// A "Book of the Month" run. Current when enabled, the book is active and
/// today's calendar date falls within [StartDate, EndDate].
/// </summary>
public class MonthlyCampaign
{
    public int Id { get; set; }
    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Correct answers (out of 10) needed to earn the reward. 1..10.</summary>
    public int PassingScore { get; set; }

    public string RewardTitle { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
