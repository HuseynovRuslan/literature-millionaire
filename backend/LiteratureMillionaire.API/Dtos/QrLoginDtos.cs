using System.ComponentModel.DataAnnotations;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// A login the kiosk has just opened. <see cref="Code"/> is what goes into the QR on screen;
/// <see cref="PollSecret"/> must not - it is what proves this browser is the one that asked, so that
/// someone who photographed the QR cannot collect the identity that lands on it.
/// </summary>
public record QrLoginStartedDto(string Code, string PollSecret, DateTime ExpiresAtUtc, int SecondsToLive);

/// <summary>
/// Where a login stands. "pending" until QRLog confirms it, then "confirmed" with the employee's name
/// and phone - once, after which the code is spent.
///
/// <see cref="SignInTicket"/> is what the browser starts a quiz with. The name and phone are here only so
/// the screen can show whose sign-in landed; the server never takes them back from the browser.
/// </summary>
public record QrLoginStatusDto(string Status, string? FullName, string? PhoneNumber, string? SignInTicket = null);

/// <summary>An employee QRLog has vouched for, as held behind a sign-in ticket.</summary>
public sealed record SignedInIdentity(string FullName, string PhoneNumber);

/// <summary>
/// What QRLog's server posts to confirm a code. It is signed, because this endpoint is the one place
/// where someone could otherwise claim to be any employee they like.
/// </summary>
public class QrLoginConfirmDto
{
    [Required, StringLength(64, MinimumLength = 8)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required, StringLength(32, MinimumLength = 7)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>ISO 8601 UTC, round-trip format. Confirmations outside a few minutes are refused.</summary>
    [Required, StringLength(40)]
    public string Timestamp { get; set; } = string.Empty;

    /// <summary>Hex HMAC-SHA256 of "code\nphoneNumber\ntimestamp" with the shared secret.</summary>
    [Required, StringLength(128, MinimumLength = 64)]
    public string Signature { get; set; } = string.Empty;
}
