namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// One question as it is replayed on the result screen, after the quiz is over.
///
/// This is the only place the correct answer is ever disclosed: it travels inside
/// <see cref="QuizResultDto"/>, which is set only when the last question has closed, so no
/// in-game response can carry it. Options are named as the player saw them in this session
/// (the per-session shuffle), not as they are stored.
/// </summary>
public record QuizAnswerReviewDto(
    // 1-based position in this session.
    int QuestionNumber,
    string Text,
    string? ImageUrl,
    string? ImageAltText,
    // Display letter (A-D) of the right option, and its text.
    string CorrectOption,
    string CorrectAnswer,
    // What the player picked; null when the clock closed the question with nothing selected.
    string? SelectedOption,
    string? SelectedAnswer,
    bool IsCorrect,
    // True when the question was closed by the deadline, including an answer that arrived late.
    bool TimedOut,
    string? Explanation,
    Entities.Difficulty Difficulty,
    // What a correct answer to this question was worth.
    int Points);
