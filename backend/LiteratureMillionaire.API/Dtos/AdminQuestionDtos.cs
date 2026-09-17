using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Dtos;

/// <summary>One question as the editor lists and edits it.</summary>
public sealed record AdminQuestionDto(
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
    int? BookId,
    string? BookTitle,
    int? QuizModeId,
    string? QuizModeTitle,
    string? ImageUrl,
    string? ImageAltText,
    string? ImageSource,
    string? ImageLicense,
    DateTime CreatedAt);

/// <summary>A page of questions, with what the filters matched in total.</summary>
public sealed record AdminQuestionPageDto(int Total, int Skip, int Take, IReadOnlyList<AdminQuestionDto> Questions);

/// <summary>A question as the form sends it. Checked by AdminQuestionService, which answers in the panel's language.</summary>
public sealed class AdminQuestionInput
{
    public string? Text { get; set; }
    public string? OptionA { get; set; }
    public string? OptionB { get; set; }
    public string? OptionC { get; set; }
    public string? OptionD { get; set; }
    /// <summary>"A", "B", "C" or "D".</summary>
    public string? CorrectOption { get; set; }
    public Difficulty? Difficulty { get; set; }
    public string? Category { get; set; }
    public string? Explanation { get; set; }
    public int? BookId { get; set; }
    public int? QuizModeId { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageAltText { get; set; }
    public string? ImageSource { get; set; }
    public string? ImageLicense { get; set; }
}

/// <summary>What the editor's filters can be built from, and how many questions each bank holds.</summary>
public sealed record AdminQuestionOptionsDto(
    IReadOnlyList<AdminQuestionBankDto> Banks,
    IReadOnlyList<AdminQuizModeOptionDto> QuizModes,
    IReadOnlyList<string> Categories,
    int EasyPerQuiz,
    int MediumPerQuiz,
    int HardPerQuiz);

/// <summary>A bank with the counts a round is planned from, so the editor can show what deleting would cost.</summary>
public sealed record AdminQuestionBankDto(int Id, string Title, bool IsActive, int Easy, int Medium, int Hard, int Images);
