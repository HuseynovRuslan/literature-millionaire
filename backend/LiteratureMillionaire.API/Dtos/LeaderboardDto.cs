namespace LiteratureMillionaire.API.Dtos;

/// <summary>A point-in-time leaderboard for one campaign.</summary>
public record LeaderboardDto(
    int CampaignId,
    DateTime GeneratedAtUtc,
    IReadOnlyList<LeaderboardEntryDto> Entries);

/// <summary>Public, privacy-safe representation of a participant's best completed attempt.</summary>
public record LeaderboardEntryDto(
    int Rank,
    string DisplayName,
    int PointsEarned,
    int MaxPoints,
    int CorrectAnswers,
    int TotalQuestions,
    double DurationSeconds,
    DateTime CompletedAtUtc);
