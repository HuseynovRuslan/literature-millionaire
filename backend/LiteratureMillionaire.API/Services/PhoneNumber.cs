using System.Text.RegularExpressions;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Azerbaijani mobile numbers. Accepts "0XXXXXXXXX", "994XXXXXXXXX" and "+994XXXXXXXXX"
/// (spaces, dashes and parentheses ignored) and normalises to "+994XXXXXXXXX".
/// Never log the input or the result: it is personal data.
/// </summary>
public static partial class PhoneNumber
{
    public const string CountryCode = "+994";

    // Nine national digits: a mobile operator prefix followed by seven digits.
    [GeneratedRegex(@"^(?:\+?994|0)((?:10|50|51|55|60|70|77|99)\d{7})$")]
    private static partial Regex MobileRegex();

    public static bool TryNormalize(string? input, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;

        var compact = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '-' && c != '(' && c != ')').ToArray());
        var match = MobileRegex().Match(compact);
        if (!match.Success) return false;

        normalized = CountryCode + match.Groups[1].Value;
        return true;
    }

    /// <summary>
    /// "+994 55 *** ** 67": enough to tell two colleagues apart on a screen, not enough to call someone.
    /// Anything that is not a normalisable number is masked completely.
    /// </summary>
    public static string Mask(string? phoneNumber) =>
        TryNormalize(phoneNumber, out var n) ? $"{CountryCode} {n.Substring(4, 2)} *** ** {n[^2..]}" : "***";
}
