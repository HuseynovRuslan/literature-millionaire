using System.Net.Mail;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Email addresses, for players who sign in with an emailed code instead of QRLog. Normalised to the trimmed,
/// lower-case address, so "Ali@Mail.az " and "ali@mail.az" are one participant and one attempt.
/// Never log the input or the result: it is personal data.
/// </summary>
public static class EmailAddress
{
    /// <summary>The longest address SMTP can carry (RFC 5321); also the column length.</summary>
    public const int MaxLength = 254;

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var candidate = input.Trim().ToLowerInvariant();
        // MailAddress also accepts "Name <a@b.az>" and quoted forms; only a bare address that parses back to
        // itself is taken, with a dot in the domain so a typo like "ali@gmail" is caught before a code is sent.
        if (candidate.Length > MaxLength
            || !MailAddress.TryCreate(candidate, out var parsed)
            || parsed.Address != candidate
            || parsed.Host.IndexOf('.') is <= 0
            || parsed.Host.EndsWith('.'))
        {
            return false;
        }

        normalized = candidate;
        return true;
    }

    /// <summary>"a***@gmail.com": enough to recognise one's own address on a screen, not enough to write to it.</summary>
    public static string Mask(string? email)
    {
        if (!TryNormalize(email, out var n)) return "***";
        var at = n.IndexOf('@');
        return $"{n[0]}***{n[at..]}";
    }
}
