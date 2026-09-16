namespace LiteratureMillionaire.API.Dtos;

/// <summary>Kiosk-facing view of the campaign that is running today.</summary>
public record CurrentCampaignDto(
    int CampaignId,
    DateOnly StartDate,
    DateOnly EndDate,
    int PassingScore,
    string RewardTitle,
    int QuestionCount,
    // Null only when the campaign has no book (possible for quiz modes other than "Ayın Kitabı").
    CampaignBookDto? Book);

public record CampaignBookDto(
    int Id,
    string Title,
    string Author,
    string Description,
    string CoverImageUrl);
