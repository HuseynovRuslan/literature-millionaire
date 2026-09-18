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

    /// <summary>
    /// Shown to players as a test version: the bank is playable but still being worked on.
    ///
    /// A player who meets a half-finished bank with no warning reads it as a broken product rather than an
    /// unfinished one, and says so to everyone else. Saying it first costs a line on the card and buys the
    /// freedom to publish early. It lived in the frontend's code until the panel could set it, which meant a
    /// deployment to take the label off a bank that was finished.
    /// </summary>
    public bool IsPreview { get; set; }

    // \z rather than $: $ would also accept a trailing newline.
    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();

    /// <summary>Application-side slug check; PostgreSQL enforces the same pattern with a CHECK constraint.</summary>
    public static bool IsValidSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && slug.Length <= SlugMaxLength && SlugPattern().IsMatch(slug);

    /// <summary>
    /// The icons the frontend can draw (QuizModeIcon's allowlist). A key outside this list renders as a
    /// generic glyph, which looks like a bug rather than a choice - so the panel offers only these.
    /// </summary>
    public static readonly IReadOnlyList<string> IconKeys = ["globe", "book", "feather", "leaf", "sprout"];
}
