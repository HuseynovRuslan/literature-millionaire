namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// Progression after an answer or a timeout. Deliberately silent about correctness:
/// no per-question verdict, correct option, explanation or running score is
/// disclosed while the quiz is active. Only the final <see cref="Result"/> is scored.
/// </summary>
public record AnswerResultDto(
    // 1-based number of the question that was just closed.
    int QuestionNumber,
    // True when the question was closed by the deadline (timeout call, or an answer that arrived late).
    bool TimedOut,
    bool IsGameOver,
    // Set only when another question follows.
    int? NextQuestionNumber,
    GameQuestionDto? NextQuestion,
    DateTime? NextQuestionExpiresAtUtc,
    // The same deadline as a duration from the moment this response was written; see StartGameResponseDto.
    int? NextQuestionRemainingMs,
    // Set only when IsGameOver.
    QuizResultDto? Result);
