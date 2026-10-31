using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>
/// "Email ilə daxil ol". Three endpoints: whether it is on, mail a code, check the code. See
/// <see cref="IEmailLoginService"/> for why the exchange is shaped so.
/// </summary>
[ApiController]
[Route("api/email-login")]
[Produces("application/json")]
public class EmailLoginController : ControllerBase
{
    private readonly IEmailLoginService _logins;

    public EmailLoginController(IEmailLoginService logins) => _logins = logins;

    /// <summary>Whether email sign-in is available (a mailbox is configured).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(EmailLoginStatusDto), StatusCodes.Status200OK)]
    public ActionResult<EmailLoginStatusDto> Status() => Ok(new EmailLoginStatusDto(_logins.IsEnabled));

    /// <summary>Mails a six-digit code to the address.</summary>
    /// <remarks>
    /// Problem codes: INVALID_NAME, INVALID_EMAIL (400); EMAIL_CODE_COOLDOWN, EMAIL_CODE_LIMIT with retryAfterSeconds
    /// (429); EMAIL_SEND_FAILED (502); EMAIL_LOGIN_DISABLED (503).
    /// </remarks>
    [HttpPost("start")]
    [ProducesResponseType(typeof(EmailLoginStartedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EmailLoginStartedDto>> Start([FromBody] EmailLoginStartDto body, CancellationToken ct)
    {
        try
        {
            return Ok(await _logins.StartAsync(body.FullName, body.Email, ct));
        }
        catch (EmailLoginException ex)
        {
            return ToProblem(ex);
        }
    }

    /// <summary>Checks the code; on a match returns the sign-in ticket the quiz is started with.</summary>
    /// <remarks>Problem codes: CODE_INVALID with attemptsLeft (400); LOGIN_NOT_FOUND (404); TOO_MANY_ATTEMPTS (429).</remarks>
    [HttpPost("verify")]
    [ProducesResponseType(typeof(EmailLoginVerifiedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public ActionResult<EmailLoginVerifiedDto> Verify([FromBody] EmailLoginVerifyDto body)
    {
        try
        {
            return Ok(_logins.Verify(body.LoginId, body.Code));
        }
        catch (EmailLoginException ex)
        {
            return ToProblem(ex);
        }
    }

    private ObjectResult ToProblem(EmailLoginException ex)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: ex.StatusCode, title: "Email sign-in", detail: ex.Message);
        problem.Extensions["code"] = ex.Code;
        if (ex.RetryAfterSeconds is { } seconds)
        {
            problem.Extensions["retryAfterSeconds"] = seconds;
            Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        if (ex.AttemptsLeft is { } left)
        {
            problem.Extensions["attemptsLeft"] = left;
        }

        return StatusCode(ex.StatusCode, problem);
    }
}
