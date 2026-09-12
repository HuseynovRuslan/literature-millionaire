namespace LiteratureMillionaire.API.Dtos;

/// <summary>Final outcome of a campaign quiz, computed server-side only.</summary>
public record QuizResultDto(
    int CorrectAnswers,
    int TotalQuestions,
    int PassingScore,
    bool Passed,
    // Set only when Passed; a failed quiz never carries the reward.
    string? RewardTitle);
