using System.Text.RegularExpressions;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// The picture addresses this site will serve: files it ships with, and files uploaded in the admin panel
/// (see <see cref="ImageUploadService"/>). Anything else - another site, a data URL, a path with ".." in it -
/// is refused wherever a picture is chosen, so a stored address can never point somewhere we do not control.
/// </summary>
public static partial class LocalImagePath
{
    /// <summary>A book cover: one of the shipped /covers files, or an uploaded one. Empty means "no cover".</summary>
    public static bool IsCover(string? path) =>
        string.IsNullOrWhiteSpace(path) || Matches(path, CoverRegex());

    /// <summary>A question illustration: a shipped /question-images file, or an uploaded one.</summary>
    public static bool IsQuestionImage(string? path) =>
        !string.IsNullOrWhiteSpace(path) && Matches(path, QuestionRegex());

    private static bool Matches(string path, Regex pattern) =>
        !path.Contains("..", StringComparison.Ordinal) && pattern.IsMatch(path.Trim());

    // \z rather than $, which would also accept a trailing newline. No "/" inside the file name, so a
    // nested path cannot be smuggled in; uploaded files are named by their content hash.
    [GeneratedRegex(@"^(/covers/[A-Za-z0-9._-]+\.(webp|png|jpg|jpeg)|/uploads/covers/[0-9a-f]{32}\.webp)\z", RegexOptions.IgnoreCase)]
    private static partial Regex CoverRegex();

    [GeneratedRegex(@"^(/question-images/[A-Za-z0-9._-]+\.(webp|png|jpg|jpeg)|/uploads/questions/[0-9a-f]{32}\.webp)\z", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionRegex();
}
