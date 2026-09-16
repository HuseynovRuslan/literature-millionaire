using System.Globalization;

namespace LiteratureMillionaire.API.Services;

/// <summary>Produces a stable public display name without changing the stored full name.</summary>
public static class LeaderboardNameMasker
{
    public static string Mask(string? fullName)
    {
        var words = (fullName ?? string.Empty)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (words.Length == 0)
        {
            return "***";
        }

        if (words.Length == 1)
        {
            return $"{StringInfo.GetNextTextElement(words[0])}***";
        }

        return $"{words[0]} {StringInfo.GetNextTextElement(words[^1])}.";
    }
}
