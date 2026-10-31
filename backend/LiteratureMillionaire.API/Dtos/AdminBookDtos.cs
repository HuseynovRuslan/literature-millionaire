namespace LiteratureMillionaire.API.Dtos;

/// <summary>A book as the admin panel lists it, with what depends on it.</summary>
public sealed record AdminBookDto(
    int Id,
    string Title,
    string Author,
    string Description,
    string CoverImageUrl,
    bool IsActive,
    int QuestionCount,
    int CampaignCount,
    // Campaigns this book is played in, newest first, so switching it off is a decision with the facts in view.
    IReadOnlyList<AdminBookCampaignDto> Campaigns);

public sealed record AdminBookCampaignDto(int Id, string QuizModeTitle, DateOnly StartDate, DateOnly EndDate, bool IsEnabled);

/// <summary>A book as the form sends it. Checked by AdminBookService, which answers in the panel's language.</summary>
public sealed class AdminBookInput
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public string? Description { get; set; }
    /// <summary>A cover from the picture library, or empty for none.</summary>
    public string? CoverImageUrl { get; set; }
    public bool IsActive { get; set; }
}
