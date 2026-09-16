using System.Text.RegularExpressions;

namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// A game category the participant can pick (Bilik Dünyası, Ayın Kitabı, ...). Every campaign belongs to one
/// mode and questions carry the mode they are played in. Not the same as <see cref="Question.Category"/>, which is
/// the source sub-category of a question (Flags, Capitals, ...).
/// </summary>
public partial class QuizMode
{
    public const int SlugMaxLength = 60;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 500;
    public const int IconKeyMaxLength = 40;

    public int Id { get; set; }

    /// <summary>Stable lowercase identifier: a-z, 0-9 and single inner hyphens, e.g. "bilik-dunyasi". Unique.</summary>
    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Key of an icon drawn locally by the frontend (never a URL).</summary>
    public string IconKey { get; set; } = string.Empty;

    /// <summary>Order in which modes are offered to the participant (ascending).</summary>
    public int DisplayOrder { get; set; }

    /// <summary>Inactive modes are hidden and their campaigns cannot be played.</summary>
    public bool IsActive { get; set; } = true;

    // \z rather than $: $ would also accept a trailing newline.
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    /// <summary>Application-side slug check; PostgreSQL enforces the same pattern with a CHECK constraint.</summary>
    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= SlugMaxLength && SlugPattern().IsMatch(slug);
}
