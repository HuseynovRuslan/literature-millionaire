using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// Player-facing question shape. Deliberately excludes <c>CorrectOption</c> and
/// <c>Explanation</c> so that the answer can never leak to the client during gameplay.
/// Options may be delivered in a per-session shuffled order (see <see cref="FromEntity(Question, int[])"/>).
/// </summary>
public record GameQuestionDto(
    int Id,
    string Text,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    Difficulty Difficulty,
    string Category,
    // Optional illustration (local asset path) and its alt text; never derived from the answer.
    string? ImageUrl,
    string? ImageAltText)
{
    public static GameQuestionDto FromEntity(Question q) => FromEntity(q, new[] { 0, 1, 2, 3 });

    /// <summary>Display slot i shows the original option at index <paramref name="optionOrder"/>[i] (0 = A .. 3 = D).</summary>
    public static GameQuestionDto FromEntity(Question q, int[] optionOrder)
    {
        var original = new[] { q.OptionA, q.OptionB, q.OptionC, q.OptionD };
        return new GameQuestionDto(
            q.Id,
            q.Text,
            original[optionOrder[0]],
            original[optionOrder[1]],
            original[optionOrder[2]],
            original[optionOrder[3]],
            q.Difficulty,
            q.Category,
            q.ImageUrl,
            q.ImageAltText);
    }
}
