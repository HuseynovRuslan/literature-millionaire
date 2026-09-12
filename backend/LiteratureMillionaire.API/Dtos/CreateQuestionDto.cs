using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Dtos;

public partial class CreateQuestionDto : IValidatableObject
{
    [Required, MaxLength(1000)]
    public string Text { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OptionA { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OptionB { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OptionC { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string OptionD { get; set; } = string.Empty;

    /// <summary>Exactly one of: A, B, C, D (upper-case).</summary>
    [Required, RegularExpression("^[ABCD]$", ErrorMessage = "CorrectOption must be A, B, C or D.")]
    public string CorrectOption { get; set; } = string.Empty;

    /// <summary>Easy, Medium or Hard. Nullable so that a missing value is rejected instead of defaulting.</summary>
    [Required, EnumDataType(typeof(Difficulty))]
    public Difficulty? Difficulty { get; set; }

    [Required, MaxLength(100)]
    public string Category { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Explanation { get; set; }

    /// <summary>Owning book. Nullable in the DTO so a missing value is rejected; must reference an existing book (active or not).</summary>
    [Required, Range(1, int.MaxValue, ErrorMessage = "BookId must be greater than zero.")]
    public int? BookId { get; set; }

    /// <summary>Optional local illustration path, e.g. "/question-images/example.webp". Requires ImageAltText.</summary>
    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    /// <summary>Alt text for the illustration. Requires ImageUrl.</summary>
    [MaxLength(300)]
    public string? ImageAltText { get; set; }

    // Only a local asset under /question-images/ with a simple file name and an image extension.
    // The character class excludes "/", "\", "?", "#", ":" so protocols, query strings, fragments,
    // nested folders and protocol-relative or data URLs cannot match; ".." is rejected separately.
    [GeneratedRegex(@"^/question-images/[A-Za-z0-9._-]+\.(webp|png|jpg|jpeg)$", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePathRegex();

    public static bool IsValidImagePath(string path) =>
        ImagePathRegex().IsMatch(path) && !path.Contains("..");

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var url = string.IsNullOrWhiteSpace(ImageUrl) ? null : ImageUrl.Trim();
        var alt = string.IsNullOrWhiteSpace(ImageAltText) ? null : ImageAltText.Trim();

        if (url is null && alt is not null)
        {
            yield return new ValidationResult("ImageAltText requires ImageUrl.", new[] { nameof(ImageAltText) });
        }

        if (url is not null && alt is null)
        {
            yield return new ValidationResult("ImageUrl requires a non-empty ImageAltText.", new[] { nameof(ImageAltText) });
        }

        if (url is not null && !IsValidImagePath(url))
        {
            yield return new ValidationResult(
                "ImageUrl must be a local asset like /question-images/name.webp (webp, png, jpg or jpeg; no folders, query strings, fragments or external URLs).",
                new[] { nameof(ImageUrl) });
        }
    }
}
