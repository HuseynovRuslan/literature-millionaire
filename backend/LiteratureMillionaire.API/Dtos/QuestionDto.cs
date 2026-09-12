using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>Full question representation for admin use (includes the correct answer).</summary>
public record QuestionDto(
    int Id,
    string Text,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string CorrectOption,
    Difficulty Difficulty,
    string Category,
    string? Explanation,
    DateTime CreatedAt,
    /* null only for legacy questions that have not been assigned to a book yet */
    int? BookId,
    string? BookTitle,
    /* optional illustration; both null or both set */
    string? ImageUrl,
    string? ImageAltText);
