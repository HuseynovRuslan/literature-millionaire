namespace LiteratureMillionaire.API.Dtos;

/// <summary>A category (quiz mode) as the admin panel lists it, with what depends on it.</summary>
/// <param name="Slug">Fixed: questions, campaigns and the card artwork all key on it, so it is shown, never edited.</param>
/// <param name="IsPreview">The bank is playable but unfinished; the card says so to players.</param>
/// <param name="RequiresBook">"Ayın Kitabı" is played per book; the campaign form asks for one.</param>
public sealed record AdminCategoryDto(
    int Id,
    string Slug,
    string Title,
    string Description,
    string IconKey,
    int DisplayOrder,
    bool IsActive,
    bool IsPreview,
    bool RequiresBook,
    int QuestionCount,
    int CampaignCount,
    bool HasRunningCampaign);

/// <summary>A category as the form sends it. The slug and the order are not here: one is fixed, the other is moved.</summary>
public sealed class AdminCategoryInput
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? IconKey { get; set; }
    public bool IsActive { get; set; }
    public bool IsPreview { get; set; }
}
