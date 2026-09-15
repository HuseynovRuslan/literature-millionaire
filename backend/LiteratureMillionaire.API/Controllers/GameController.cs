using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

[ApiController]
[Route("api/game")]
[Produces("application/json")]
public class GameController : ControllerBase
{
    private readonly IGameService _game;

    public GameController(IGameService game)
    {
        _game = game;
    }

    /// <summary>Legacy Millionaire-era prize ladder. Not used by the campaign quiz; kept for compatibility until removed.</summary>
    [HttpGet("prizes")]
    [ProducesResponseType(typeof(IReadOnlyList<int>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<int>> Prizes() => Ok(PrizeLadder.All);

    /// <summary>Start a new "Ayın kitabı" quiz (10 questions from the active campaign's book) and receive the first question.</summary>
    /// <remarks>404 with code NO_ACTIVE_CAMPAIGN when no campaign is current; 409 with code INSUFFICIENT_CAMPAIGN_QUESTIONS when the book has too few questions.</remarks>
    /// <remarks>Body: fullName + phoneNumber. 409 with code ATTEMPT_LIMIT_REACHED (maxAttempts, attemptsUsed) once the number has used all attempts for the campaign.</remarks>
    [HttpPost("start")]
    [ProducesResponseType(typeof(StartGameResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StartGameResponseDto>> Start([FromBody] StartGameRequestDto request, CancellationToken ct)
    {
        try
        {
            return Ok(await _game.StartAsync(request, ct));
        }
        catch (GameException ex)
        {
            return ToProblem(ex);
        }
        catch (CampaignException ex)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
            problem.Extensions["code"] = ex.Code;
            return StatusCode(ex.StatusCode, problem);
        }
    }

    /// <summary>Submit an answer for the session's current question. Late answers count as timed out; correctness is not disclosed until the quiz ends.</summary>
    [HttpPost("{sessionId:guid}/answer")]
    [ProducesResponseType(typeof(AnswerResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AnswerResultDto>> Answer(Guid sessionId, [FromBody] SubmitAnswerDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _game.AnswerAsync(sessionId, dto, ct));
        }
        catch (GameException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>
    /// Close the current question as unanswered once its server deadline has passed.
    /// Before the deadline this returns 409 with code QUESTION_TIME_REMAINING.
    /// </summary>
    [HttpPost("{sessionId:guid}/timeout")]
    [ProducesResponseType(typeof(AnswerResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AnswerResultDto>> Timeout(Guid sessionId, [FromBody] TimeoutRequestDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _game.TimeoutAsync(sessionId, dto, ct));
        }
        catch (GameException ex)
        {
            return ToProblem(ex);
        }
    }

    private ObjectResult ToProblem(GameException ex)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);

        if (ex.Extensions is not null)
        {
            foreach (var (key, value) in ex.Extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return StatusCode(ex.StatusCode, problem);
    }
}
