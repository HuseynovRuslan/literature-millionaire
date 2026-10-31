using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Campaigns in the admin panel. Every change is recorded in the audit trail by the service.</summary>
[ApiController]
[Route("api/admin/campaigns")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminCampaignsController : ControllerBase
{
    private readonly IAdminCampaignService _campaigns;

    public AdminCampaignsController(IAdminCampaignService campaigns) => _campaigns = campaigns;

    /// <summary>Every campaign, running ones first, with its problems spelled out.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCampaignDto>>> List(CancellationToken ct) =>
        Ok(await _campaigns.ListAsync(ct));

    /// <summary>Categories, books and question counts for the campaign form.</summary>
    [HttpGet("options")]
    public async Task<ActionResult<AdminCampaignOptionsDto>> Options(CancellationToken ct) =>
        Ok(await _campaigns.OptionsAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(AdminCampaignDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AdminCampaignInput input, CancellationToken ct)
    {
        try
        {
            var created = await _campaigns.CreateAsync(input, AdminAuth.Actor(User), ct);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (AdminCampaignValidationException ex)
        {
            return Invalid(ex);
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AdminCampaignInput input, CancellationToken ct)
    {
        try
        {
            var updated = await _campaigns.UpdateAsync(id, input, AdminAuth.Actor(User), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AdminCampaignValidationException ex)
        {
            return Invalid(ex);
        }
    }

    private ObjectResult Invalid(AdminCampaignValidationException ex)
    {
        var problem = new ValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Kampaniya yadda saxlanmadı.",
        };
        problem.Extensions["code"] = "CAMPAIGN_INVALID";
        return BadRequest(problem);
    }
}
