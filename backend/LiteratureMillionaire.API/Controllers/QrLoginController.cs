using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>
/// "QRLog ilə davam et". Three endpoints: the kiosk opens a login, QRLog's server confirms it, the
/// kiosk polls for the result. See <see cref="IQrLoginService"/> for why the exchange is shaped so.
/// </summary>
[ApiController]
[Route("api/qrlog-login")]
[Produces("application/json")]
public class QrLoginController : ControllerBase
{
    private readonly IQrLoginService _logins;
    private readonly ILogger<QrLoginController> _logger;

    public QrLoginController(IQrLoginService logins, ILogger<QrLoginController> logger)
    {
        _logins = logins;
        _logger = logger;
    }

    /// <summary>Opens a login. The code goes into the QR; the poll secret stays in this browser.</summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(QrLoginStartedDto), StatusCodes.Status200OK)]
    public ActionResult<QrLoginStartedDto> Start() => Ok(_logins.Start());

    /// <summary>
    /// Confirms a code on behalf of an employee. Called by QRLog's server, never by a browser or a
    /// phone: it carries the shared signature, which nothing client-side is given.
    /// </summary>
    [HttpPost("confirm")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult Confirm([FromBody] QrLoginConfirmDto dto)
    {
        if (!_logins.IsSignatureValid(dto.Code, dto.PhoneNumber, dto.Timestamp, dto.Signature))
        {
            // Never say which part was wrong: that would help someone tune an attempt.
            _logger.LogWarning("QRLog sign-in confirmation rejected: signature or timestamp did not verify.");
            return Unauthorized(new { code = "INVALID_SIGNATURE", message = "Təsdiq imzası etibarlı deyil." });
        }

        if (!_logins.Confirm(dto.Code, dto.FullName.Trim(), dto.PhoneNumber.Trim()))
        {
            return Conflict(new { code = "LOGIN_NOT_PENDING", message = "Bu QR kodun vaxtı bitib və ya artıq istifadə olunub." });
        }

        return NoContent();
    }

    /// <summary>
    /// Where the login stands. A wrong secret reads exactly like a code that does not exist, so this
    /// cannot be used to find out whether a code is real.
    /// </summary>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(QrLoginStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<QrLoginStatusDto> Poll(string code, [FromQuery] string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || _logins.Poll(code, secret) is not { } status)
        {
            return NotFound(new { code = "LOGIN_NOT_FOUND", message = "Bu giriş tapılmadı və ya vaxtı bitib." });
        }

        return Ok(status);
    }
}
