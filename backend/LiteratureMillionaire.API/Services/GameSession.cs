namespace LiteratureMillionaire.API.Services;

/// <summary>
/// One question as it exists inside a session: which database row, in which
/// display order its four options are shown, and which display letter is correct.
/// Exists only in server memory; the database rows are never changed.
/// </summary>
public sealed class SessionQuestion
{
    public required int QuestionId { get; init; }

    /// <summary>Display slot i (0 = A .. 3 = D) shows the original option at index OptionOrder[i].</summary>
    public required int[] OptionOrder { get; init; }

    /// <summary>Correct answer as the player sees it in this session: A, B, C or D.</summary>
    public required char CorrectDisplayOption { get; init; }
}

/// <summary>
/// In-memory state of one campaign quiz. Lives only in IMemoryCache for this MVP.
/// Everything needed to judge the result is kept here, never taken from the client.
/// </summary>
public class GameSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();

    public required int CampaignId { get; init; }
    public required int BookId { get; init; }
    public required int PassingScore { get; init; }
    public required string RewardTitle { get; init; }

    /// <summary>Ordered questions for this session (QuizRules.QuestionsPerQuiz of them, all from BookId).</summary>
    public required IReadOnlyList<SessionQuestion> Questions { get; init; }

    /// <summary>0-based index of the question the player must answer next.</summary>
    public int CurrentIndex { get; set; }

    /// <summary>UTC instant after which the current question can no longer be answered.</summary>
    public DateTime QuestionDeadlineUtc { get; set; }

    public int CorrectAnswers { get; set; }

    public bool IsGameOver { get; set; }

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    /// <summary>Serializes answer and timeout submissions for the same session.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>1-based number of the current question.</summary>
    public int CurrentQuestionNumber => CurrentIndex + 1;

    public SessionQuestion Current => Questions[CurrentIndex];

    public int CurrentQuestionId => Current.QuestionId;

    public int TotalQuestions => Questions.Count;

    public bool Passed => CorrectAnswers >= PassingScore;
}
