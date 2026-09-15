namespace LiteratureMillionaire.API.Entities;

public class Question
{
    public int Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;

    /// <summary>Single character: A, B, C or D.</summary>
    public char CorrectOption { get; set; }

    public Difficulty Difficulty { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Explanation { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Owning book. Nullable only during the expand-contract transition: legacy
    /// questions created before books existed stay unassigned until classified.
    /// New and updated questions must always carry a BookId (enforced in the API).
    /// </summary>
    public int? BookId { get; set; }
    public Book? Book { get; set; }

    /// <summary>
    /// Quiz mode the question is played in ("bilik-dunyasi", "ayin-kitabi", ...). Null for legacy questions, which
    /// are never selected for a game. Not the same as <see cref="Category"/>, the source sub-category.
    /// </summary>
    public int? QuizModeId { get; set; }
    public QuizMode? QuizMode { get; set; }

    /// <summary>
    /// Optional illustration, a local frontend asset such as "/question-images/name.webp".
    /// Either both media fields are set or both are null (enforced in the API and by a CHECK constraint).
    /// </summary>
    public string? ImageUrl { get; set; }
    public string? ImageAltText { get; set; }
}
