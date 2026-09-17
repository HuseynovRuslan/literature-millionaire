namespace LiteratureMillionaire.API.Dtos;

public record StartGameResponseDto(
    Guid SessionId,
    int QuestionNumber,
    int TotalQuestions,
    int PassingScore,
    int SecondsPerQuestion,
    // UTC deadline for the delivered question; the server judges lateness against it.
    DateTime QuestionExpiresAtUtc,
    // The same deadline as a duration from the moment this response was written. This is what a client
    // should count down from: a duration survives the trip between two machines, an absolute time does
    // not - the player's phone and this server do not agree on what time it is, and a phone two seconds
    // slow would show two seconds the server has already spent.
    int QuestionRemainingMs,
    GameQuestionDto Question,
    // This start's attempt number (1..MaxAttempts) and how many the participant has left afterwards. No phone number here.
    int AttemptNumber,
    int RemainingAttempts,
    // Campaign being played and its quiz mode (public identifiers only).
    int CampaignId,
    QuizModeRefDto QuizMode);
