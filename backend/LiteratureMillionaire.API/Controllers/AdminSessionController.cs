using System.ComponentModel.DataAnnotations;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>
/// Signing in to the admin panel. The usual way is the same QRLog QR players use: the panel opens a sign-in,
/// QRLog vouches for the employee, and the ticket is exchanged here for a session - if, and only if, that
/// employee's phone is on the admin list. The other way is a break-glass link created on the server.
///
/// The session is an HttpOnly, Secure, SameSite=Strict cookie with an absolute eight-hour lifetime. Sessions
/// do not survive an API restart (a deployment): signing in again is a QR scan, and the alternative - keys kept
/// on disk - would be one more secret to guard for no gain here.
/// </summary>
[ApiController]
[Route("api/admin/session")]
[Produces("application/json")]
public class AdminSessionController : ControllerBase
{
    private readonly IQrLoginService _logins;
    private readonly IAdminDirectory _admins;
    private readonly IAdminAuditLog _audit;
    private readonly IAdminLoginLinks _links;
    private readonly ILogger<AdminSessionController> _logger;

    public AdminSessionController(IQrLoginService logins, IAdminDirectory admins, IAdminAuditLog audit,
        IAdminLoginLinks links, ILogger<AdminSessionController> logger)
    {
        _logins = logins;
        _admins = admins;
        _audit = audit;
        _links = links;
        _logger = logger;
    }

    public sealed class TicketSignIn
    {
        [Required, MaxLength(128)]
        public string SignInTicket { get; set; } = string.Empty;
    }

    public sealed class LinkSignIn
    {
        [Required, MaxLength(128)]
        public string Token { get; set; } = string.Empty;
    }

    public sealed record AdminSessionDto(string FullName, string Phone);

    /// <summary>Exchanges a QRLog sign-in ticket for an admin session.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SignIn([FromBody] TicketSignIn body, CancellationToken ct)
    {
        if (_logins.ConsumeTicket(body.SignInTicket) is not { } identity)
        {
            return Problem401("SIGN_IN_EXPIRED", "QRLog girişinin vaxtı bitib. QR kodu yenidən oxudun.");
        }

        return await OpenSessionAsync(new AdminActor(identity.FullName, identity.PhoneNumber), "sign-in", ct);
    }

    /// <summary>Redeems a break-glass link created on the server with --admin-link.</summary>
    [HttpPost("link")]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SignInWithLink([FromBody] LinkSignIn body, CancellationToken ct)
    {
        if (await _links.RedeemAsync(body.Token, ct) is not { } actor)
        {
            return Problem401("LINK_INVALID", "Bu giriş linki etibarsızdır, artıq istifadə olunub və ya vaxtı bitib.");
        }

        return await OpenSessionAsync(actor, "sign-in-link", ct);
    }

    /// <summary>Who is signed in.</summary>
    [HttpGet]
    [Authorize(Policy = AdminAuth.Policy)]
    [ProducesResponseType(typeof(AdminSessionDto), StatusCodes.Status200OK)]
    public ActionResult<AdminSessionDto> Current()
    {
        var actor = AdminAuth.Actor(User);
        return Ok(new AdminSessionDto(actor.FullName, PhoneNumber.Mask(actor.PhoneNumber)));
    }

    /// <summary>Ends the session.</summary>
    [HttpDelete]
    [Authorize(Policy = AdminAuth.Policy)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SignOut(CancellationToken ct)
    {
        await _audit.RecordAsync(AdminAuth.Actor(User), "sign-out", ct: ct);
        await HttpContext.SignOutAsync(AdminAuth.Scheme);
        return NoContent();
    }

    private async Task<IActionResult> OpenSessionAsync(AdminActor actor, string action, CancellationToken ct)
    {
        if (!_admins.IsAdmin(actor.PhoneNumber))
        {
            // Recorded, because "somebody who is not an admin tried to get in" is exactly what an audit trail
            // is for. The log line carries no phone: the audit table is the one place that holds it.
            await _audit.RecordAsync(actor, action + "-denied", ct: ct);
            _logger.LogWarning("Admin sign-in refused: the confirmed phone is not on the admin list.");
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status403Forbidden,
                "Not an administrator", detail: "Bu nömrə idarəetmə panelinə giriş siyahısında deyil.");
            problem.Extensions["code"] = "NOT_AN_ADMIN";
            return StatusCode(StatusCodes.Status403Forbidden, problem);
        }

        await HttpContext.SignInAsync(AdminAuth.Scheme, AdminAuth.Principal(actor.FullName, actor.PhoneNumber),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = false });
        await _audit.RecordAsync(actor, action, ct: ct);
        return Ok(new AdminSessionDto(actor.FullName, PhoneNumber.Mask(actor.PhoneNumber)));
    }

    private ObjectResult Problem401(string code, string detail)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status401Unauthorized,
            "Admin sign-in failed", detail: detail);
        problem.Extensions["code"] = code;
        return StatusCode(StatusCodes.Status401Unauthorized, problem);
    }
}
