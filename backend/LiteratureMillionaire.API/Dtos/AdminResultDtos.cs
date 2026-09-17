namespace LiteratureMillionaire.API.Dtos;

/// <summary>Everything played in one campaign, as the admin panel shows it.</summary>
public sealed record AdminCampaignResultsDto(
    AdminResultsCampaignDto Campaign,
    int Started,
    int Completed,
    int Passed,
    int Unfinished,
    // Rows matching the search (all rows without one): ranked completed attempts first, then unfinished ones, newest first.
    IReadOnlyList<AdminAttemptRowDto> Attempts);

public sealed record AdminResultsCampaignDto(
    int Id,
    string QuizModeTitle,
    string? BookTitle,
    DateOnly StartDate,
    DateOnly EndDate,
    int PassingScore,
    string RewardTitle);

public sealed record AdminAttemptRowDto(
    int AttemptId,
    int ParticipantId,
    // Null for an unfinished attempt.
    int? Rank,
    string FullName,
    // Masked, e.g. "+994 55 *** ** 02". The full number is only in the Excel export, which is audited.
    string Phone,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int? CorrectAnswers,
    int TotalQuestions,
    int? PointsEarned,
    int MaxPoints,
    double? DurationSeconds,
    bool? Passed,
    // Attempts this participant has in every campaign, this one included: what removing them would delete.
    int ParticipantAttempts);

/// <summary>Why an attempt is reset or a participant removed. Required: the trail must say why, not only who.</summary>
public sealed class AdminReasonInput
{
    public string? Reason { get; set; }
}
