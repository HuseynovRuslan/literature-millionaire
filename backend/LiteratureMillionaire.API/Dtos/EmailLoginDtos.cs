using System.ComponentModel.DataAnnotations;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>Whether the registration screen should offer "Email ilə" next to QRLog.</summary>
public record EmailLoginStatusDto(bool Enabled);

/// <summary>Asks for a code. The name and address are checked by the service, which says what is wrong in words.</summary>
public class EmailLoginStartDto
{
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// A code is on its way. <see cref="LoginId"/> stays in this browser and is sent back with the code: a code read
/// over somebody's shoulder is useless without it.
/// </summary>
/// <param name="LoginId">Identifies this sign-in; sent back with the code.</param>
/// <param name="Email">The address as it was normalised - what the screen says the code went to.</param>
/// <param name="SecondsToLive">How long the code works.</param>
/// <param name="ResendAfterSeconds">How long before another code may be asked for.</param>
public record EmailLoginStartedDto(string LoginId, string Email, int SecondsToLive, int ResendAfterSeconds);

public class EmailLoginVerifyDto
{
    [Required, StringLength(64, MinimumLength = 16)]
    public string LoginId { get; set; } = string.Empty;

    [MaxLength(32)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// The code matched. <see cref="SignInTicket"/> is what the quiz is started with; the name and address are only
/// for the welcome screen - the server takes who plays from the ticket.
/// </summary>
public record EmailLoginVerifiedDto(string FullName, string Email, string SignInTicket);
