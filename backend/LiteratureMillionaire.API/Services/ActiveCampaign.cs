namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Server-side view of a campaign that can be played today, as used to start a quiz. Every rule here is read
/// from the database, never from the client. Internal only: never serialized as-is.
/// </summary>
public sealed record ActiveCampaign(
    int CampaignId,
    int PassingScore,
    string RewardTitle,
    int ImageQuestionsPerQuiz,
    int QuizModeId,
    string QuizModeSlug,
    string QuizModeTitle,
    int? BookId);
