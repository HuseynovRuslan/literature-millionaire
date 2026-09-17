using System.ComponentModel.DataAnnotations;
using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// Who is starting the quiz. Validated here; the phone is normalised before it is stored or compared.
///
/// A player starts with <see cref="SignInTicket"/>, from a confirmed QRLog sign-in, and the server takes the
/// name and phone from that ticket. <see cref="FullName"/> and <see cref="PhoneNumber"/> are only read when no
/// ticket is sent, which production refuses (Game:RequireQrLogin); they remain for development and tests.
/// </summary>
public class StartGameRequestDto : IValidatableObject
{
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Azerbaijani mobile number: 0XXXXXXXXX, 994XXXXXXXXX or +994XXXXXXXXX.</summary>
    [MaxLength(32)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>The ticket a confirmed QRLog sign-in returned. When present, it alone says who is playing.</summary>
    [MaxLength(128)]
    public string? SignInTicket { get; set; }

    /// <summary>
    /// Campaign to play, as listed by GET /api/campaigns/available. Omitted: the default "Bilik Dünyası" campaign
    /// (clients written before quiz modes). Rules, image target and book are always read from the campaign.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "CampaignId must be greater than zero.")]
    public int? CampaignId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // With a ticket, whatever name and phone the body carries are ignored, so they are not validated either.
        if (!string.IsNullOrWhiteSpace(SignInTicket))
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(FullName?.Trim()) || FullName.Trim().Length < 2)
        {
            yield return new ValidationResult("FullName must have at least 2 characters.", new[] { nameof(FullName) });
        }

        if (!Services.PhoneNumber.TryNormalize(PhoneNumber, out _))
        {
            yield return new ValidationResult("PhoneNumber must be an Azerbaijani mobile number (0XX XXX XX XX, 994XXXXXXXXX or +994XXXXXXXXX).", new[] { nameof(PhoneNumber) });
        }
    }
}
