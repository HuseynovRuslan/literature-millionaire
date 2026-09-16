using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;

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

    public required Difficulty Difficulty { get; init; }

    /// <summary>Points awarded for a correct, on-time answer; fixed when the session is created.</summary>
    public required int Points { get; init; }

    /// <summary>
    /// What the player picked, as a display letter (A-D), or null when the clock closed the question
    /// with nothing selected. Written once, when the question closes, and read only to build the
    /// end-of-quiz review.
    /// </summary>
    public char? SelectedDisplayOption { get; set; }

    /// <summary>True when the deadline closed this question, including an answer that arrived late.</summary>
    public bool TimedOut { get; set; }

    /// <summary>Whether the answer counted. Decided by the server when the question closed, never later.</summary>
    public bool IsCorrect { get; set; }
}

/// <summary>
/// In-memory state of one campaign quiz. Lives only in IMemoryCache for this MVP.
/// Everything needed to judge the result is kept here, never taken from the client.
/// </summary>
public class GameSession
{
    public Guid SessionId { get; init; } = Guid.NewGuid();

    public required int CampaignId { get; init; }

    /// <summary>Internal participant identity used only for the post-completion position lookup.</summary>
    public required int ParticipantId { get; init; }

    /// <summary>Database row of this attempt; its result columns are written once when the quiz ends.</summary>
    public required int AttemptId { get; init; }
    /// <summary>Book the questions were drawn from, when the campaign has one.</summary>
    public required int? BookId { get; init; }

    /// <summary>Quiz mode of the campaign (public id, slug and title only).</summary>
    public required QuizModeRefDto QuizMode { get; init; }
    public required int PassingScore { get; init; }
    public required string RewardTitle { get; init; }

    /// <summary>Ordered questions for this session (QuizRules.QuestionsPerQuiz of them, from the campaign's quiz mode).</summary>
    public required IReadOnlyList<SessionQuestion> Questions { get; init; }

    /// <summary>0-based index of the question the player must answer next.</summary>
    public int CurrentIndex { get; set; }

    /// <summary>UTC instant after which the current question can no longer be answered.</summary>
    public DateTime QuestionDeadlineUtc { get; set; }

    public int CorrectAnswers { get; set; }

    /// <summary>Sum of Points of correctly answered questions. Never taken from the client.</summary>
    public int PointsEarned { get; set; }

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

    public int MaxPoints => Questions.Sum(q => q.Points);

    public bool Passed => CorrectAnswers >= PassingScore;
}
