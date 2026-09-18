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
    /// <summary>
    /// The cookie that lets another window of the same browser carry on a sign-in this one started.
    ///
    /// On a phone the person leaves for QRLog and is handed back to a page that is often a different window -
    /// an installed app, a new tab - with none of this page's storage. Without this, that window minted a fresh
    /// code and waited for an approval that had already landed on the old one; the name never arrived. Cookies
    /// are the one thing every window of a browser shares. HttpOnly, so a script (or a photograph of the QR) still
    /// cannot read the poll secret; it lives as long as the code does.
    /// </summary>
    private const string PendingCookie = "kitabxana_qr";

    [HttpPost("start")]
    [ProducesResponseType(typeof(QrLoginStartedDto), StatusCodes.Status200OK)]
    public ActionResult<QrLoginStartedDto> Start()
    {
        var started = _logins.Start();
        Response.Cookies.Append(PendingCookie, $"{started.Code}.{started.PollSecret}", new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/api/qrlog-login",
            Expires = started.ExpiresAtUtc,
        });
        return Ok(started);
    }

    /// <summary>The sign-in this browser last started, if it is still alive. Read from the cookie; nothing else identifies it.</summary>
    [HttpGet("resume")]
    [ProducesResponseType(typeof(QrLoginStartedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public ActionResult<QrLoginStartedDto> Resume()
    {
        if (Request.Cookies.TryGetValue(PendingCookie, out var value) && value.Split('.', 2) is [var code, var secret]
            && _logins.Resume(code, secret) is { } started)
        {
            return Ok(started);
        }

        return NoContent();
    }

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

        // Refuse a number this quiz cannot play with, rather than hand the kiosk an identity that
        // fails at the start button. The signature is checked first, so this only ever answers a
        // caller that already holds the shared secret - it tells an integrator what is wrong, nobody else.
        if (!_logins.TryNormalizePhone(dto.PhoneNumber, out var phone))
        {
            _logger.LogWarning("QRLog sign-in confirmation rejected: the phone number is not one the quiz can use.");
            return BadRequest(new { code = "INVALID_PHONE", message = "Telefon nömrəsi yarışın tanıdığı formada deyil." });
        }

        if (!_logins.Confirm(dto.Code, dto.FullName.Trim(), phone))
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
