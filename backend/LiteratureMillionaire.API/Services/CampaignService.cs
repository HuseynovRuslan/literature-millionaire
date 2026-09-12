using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

public class CampaignService : ICampaignService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CampaignService> _logger;

    public CampaignService(ApplicationDbContext db, ILogger<CampaignService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CurrentCampaignDto> GetCurrentAsync(CancellationToken ct = default)
    {
        // Calendar date of the machine the API runs on (the kiosk environment), not UTC.
        var today = DateOnly.FromDateTime(DateTime.Now);

        // Take(2): enough to tell "one" from "more than one" without loading everything.
        var matches = await _db.MonthlyCampaigns
            .AsNoTracking()
            .Where(c => c.IsEnabled
                        && c.Book.IsActive
                        && c.StartDate <= today
                        && c.EndDate >= today)
            .OrderBy(c => c.Id)
            .Select(c => new CurrentCampaignDto(
                c.Id,
                c.StartDate,
                c.EndDate,
                c.PassingScore,
                c.RewardTitle,
                QuizRules.QuestionsPerQuiz,
                new CampaignBookDto(
                    c.Book.Id,
                    c.Book.Title,
                    c.Book.Author,
                    c.Book.Description,
                    c.Book.CoverImageUrl)))
            .Take(2)
            .ToListAsync(ct);

        if (matches.Count == 0)
        {
            throw CampaignException.NoActiveCampaign();
        }

        if (matches.Count > 1)
        {
            _logger.LogError(
                "Campaign configuration error: more than one enabled campaign covers {Today} (ids include {Ids}). Exactly one must be current.",
                today, string.Join(", ", matches.Select(m => m.CampaignId)));
            throw CampaignException.MultipleActiveCampaigns(matches.Count);
        }

        return matches[0];
    }
}
