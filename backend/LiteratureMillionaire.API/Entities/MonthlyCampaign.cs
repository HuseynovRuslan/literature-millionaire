using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// A run of one quiz mode over a date range. Playable when it is enabled, its quiz mode is active, its book (if it
/// has one) is active and today's calendar date falls within [StartDate, EndDate]. At most one playable campaign
/// per quiz mode may cover a date; campaigns of different modes may run in parallel.
/// </summary>
public class MonthlyCampaign
{
    public int Id { get; set; }

    public int QuizModeId { get; set; }
    public QuizMode QuizMode { get; set; } = null!;

    /// <summary>Book the questions come from. Required for "Ayın Kitabı" campaigns (enforced by CampaignService), optional otherwise.</summary>
    public int? BookId { get; set; }
    public Book? Book { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    /// <summary>Correct answers (out of 10) needed to earn the reward. 1..10.</summary>
    public int PassingScore { get; set; }

    public string RewardTitle { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Preferred number of illustrated questions per quiz (0..10, CHECK constraint). QuestionMixPlanner uses fewer
    /// when the pool has fewer illustrations and more only when text questions cannot fill a difficulty quota.
    /// </summary>
    public int ImageQuestionsPerQuiz { get; set; } = QuizRules.DefaultImageQuestionsPerQuiz;
}
