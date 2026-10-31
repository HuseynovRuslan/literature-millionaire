namespace LiteratureMillionaire.API.Dtos;

/// <summary>One campaign playable today, as listed by GET /api/campaigns/available.</summary>
public record CampaignSummaryDto(
    int CampaignId,
    DateOnly StartDate,
    DateOnly EndDate,
    int PassingScore,
    string RewardTitle,
    int QuestionCount,
    // Preferred number of illustrated questions per quiz for this campaign (0..10).
    int ImageQuestionsPerQuiz,
    QuizModeDto QuizMode,
    // Null when the campaign has no book (only "Ayın Kitabı" campaigns require one).
    CampaignBookDto? Book);

/// <summary>Public description of a quiz mode.</summary>
/// <param name="IsPreview">The bank is playable but unfinished; the card says so.</param>
public record QuizModeDto(
    int Id,
    string Slug,
    string Title,
    string Description,
    string IconKey,
    int DisplayOrder,
    bool IsPreview);

/// <summary>Short quiz mode reference carried by the game start response and the final result.</summary>
public record QuizModeRefDto(
    int Id,
    string Slug,
    string Title);
