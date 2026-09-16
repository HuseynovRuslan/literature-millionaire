namespace LiteratureMillionaire.API.Dtos;

/// <summary>Final outcome of a campaign quiz, computed server-side only.</summary>
public record QuizResultDto(
    int CampaignId,
    int? LeaderboardPosition,
    int CorrectAnswers,
    int TotalQuestions,
    int PassingScore,
    bool Passed,
    // Set only when Passed; a failed quiz never carries the reward.
    string? RewardTitle,
    // Weighted score: Easy 1, Medium 2, Hard 3 per correct answer. Passing is decided by CorrectAnswers, not points.
    int PointsEarned,
    int MaxPoints,
    // Quiz mode the campaign belongs to; the leaderboard stays per campaign.
    QuizModeRefDto QuizMode,
    // Every question of the session with its correct answer and the player's own. Nothing during the
    // quiz carries this; it exists only here, on the finished result.
    IReadOnlyList<QuizAnswerReviewDto> Review);
