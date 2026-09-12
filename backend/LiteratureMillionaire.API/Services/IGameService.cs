using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface IGameService
{
    /// <summary>Builds a new campaign quiz (QuizRules.QuestionsPerQuiz questions from the active campaign's book) and returns its first question.</summary>
    /// <exception cref="CampaignException">404 when no campaign is current; 500 when several are.</exception>
    /// <exception cref="GameException">409 when the book has too few questions.</exception>
    Task<StartGameResponseDto> StartAsync(CancellationToken ct = default);

    /// <summary>Checks the answer for the session's current question and advances or ends the game.</summary>
    /// <exception cref="GameException">404 unknown session; 409 game over / wrong question.</exception>
    Task<AnswerResultDto> AnswerAsync(Guid sessionId, SubmitAnswerDto dto, CancellationToken ct = default);

    /// <summary>Closes the current question as unanswered once its deadline has passed and advances the quiz.</summary>
    /// <exception cref="GameException">404 unknown session; 409 game over, wrong question, or time still remaining.</exception>
    Task<AnswerResultDto> TimeoutAsync(Guid sessionId, TimeoutRequestDto dto, CancellationToken ct = default);
}
