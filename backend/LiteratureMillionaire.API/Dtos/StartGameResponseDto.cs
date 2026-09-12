namespace LiteratureMillionaire.API.Dtos;

public record StartGameResponseDto(
    Guid SessionId,
    int QuestionNumber,
    int TotalQuestions,
    int PassingScore,
    int SecondsPerQuestion,
    // UTC deadline for the delivered question; the server judges lateness against it.
    DateTime QuestionExpiresAtUtc,
    GameQuestionDto Question);
