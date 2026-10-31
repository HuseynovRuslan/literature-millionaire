namespace LiteratureMillionaire.API.Dtos;

/// <summary>A campaign as the admin panel lists it: its settings, where it stands today and what is wrong with it.</summary>
public sealed record AdminCampaignDto(
    int Id,
    int QuizModeId,
    string QuizModeTitle,
    int? BookId,
    string? BookTitle,
    DateOnly StartDate,
    DateOnly EndDate,
    int PassingScore,
    string RewardTitle,
    int ImageQuestionsPerQuiz,
    bool IsEnabled,
    // "running", "scheduled", "ended" or "disabled".
    string Status,
    int AttemptsStarted,
    int AttemptsCompleted,
    // Plain-language problems that keep it from being played as configured; empty when there are none.
    IReadOnlyList<string> Issues);

/// <summary>What the campaign form chooses from, and how many questions each choice would draw on.</summary>
public sealed record AdminCampaignOptionsDto(
    DateOnly Today,
    IReadOnlyList<AdminQuizModeOptionDto> QuizModes,
    IReadOnlyList<AdminBookOptionDto> Books,
    IReadOnlyList<AdminQuestionPoolDto> Pools,
    int EasyPerQuiz,
    int MediumPerQuiz,
    int HardPerQuiz);

public sealed record AdminQuizModeOptionDto(int Id, string Title, bool IsActive, bool RequiresBook);

public sealed record AdminBookOptionDto(int Id, string Title, string Author, bool IsActive);

/// <summary>Questions of one quiz mode and one book (null: questions with no book). Images counts illustrated ones.</summary>
public sealed record AdminQuestionPoolDto(int QuizModeId, int? BookId, int Easy, int Medium, int Hard, int Images);

/// <summary>A campaign as the form sends it. Checked by AdminCampaignService, which answers in the panel's language.</summary>
public sealed class AdminCampaignInput
{
    public int? QuizModeId { get; set; }
    public int? BookId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int PassingScore { get; set; }
    public string? RewardTitle { get; set; }
    public int ImageQuestionsPerQuiz { get; set; }
    public bool IsEnabled { get; set; }
}
