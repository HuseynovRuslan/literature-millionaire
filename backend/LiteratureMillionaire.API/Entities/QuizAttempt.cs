namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// One started quiz for a participant in a campaign. Created at a successful start
/// (that is what consumes an attempt); result columns stay null if the quiz is abandoned.
/// </summary>
public class QuizAttempt
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;

    public int CampaignId { get; set; }
    public MonthlyCampaign Campaign { get; set; } = null!;

    /// <summary>1..QuizRules.MaxAttemptsPerCampaign (the schema's CHECK allows up to 3), unique per participant and campaign.</summary>
    public int AttemptNumber { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public int? CorrectAnswers { get; set; }
    public int? PointsEarned { get; set; }
    public bool? Passed { get; set; }

    // Rules in force when the attempt started, so history stays interpretable if rules change.
    public int TotalQuestions { get; set; }
    public int PassingScore { get; set; }
    public int MaxPoints { get; set; }
}
