using System.ComponentModel.DataAnnotations;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

[ApiController]
[Route("api/campaigns")]
[Produces("application/json")]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaigns;
    private readonly ILeaderboardService _leaderboard;

    public CampaignsController(ICampaignService campaigns, ILeaderboardService leaderboard)
    {
        _campaigns = campaigns;
        _leaderboard = leaderboard;
    }

    /// <summary>The "Bilik Dünyası" campaign running today (the default quiz mode), with its book. Kept for existing kiosk clients.</summary>
    /// <remarks>Problem responses carry a stable <c>code</c>: NO_ACTIVE_CAMPAIGN (404) or MULTIPLE_ACTIVE_CAMPAIGNS (500).</remarks>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentCampaignDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CurrentCampaignDto>> Current(CancellationToken ct)
    {
        try
        {
            return Ok(await _campaigns.GetCurrentAsync(ct));
        }
        catch (CampaignException ex)
        {
            return CampaignProblem(ex);
        }
    }

    /// <summary>Campaigns that can be played today: at most one per active quiz mode, ordered by the mode's display order.</summary>
    /// <remarks>
    /// Problem responses carry a stable <c>code</c>: NO_ACTIVE_CAMPAIGN (404) when nothing is playable,
    /// MULTIPLE_ACTIVE_CAMPAIGNS (500) when a quiz mode has more than one campaign covering today.
    /// </remarks>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IReadOnlyList<CampaignSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<CampaignSummaryDto>>> Available(CancellationToken ct)
    {
        try
        {
            return Ok(await _campaigns.GetAvailableAsync(ct));
        }
        catch (CampaignException ex)
        {
            return CampaignProblem(ex);
        }
    }

    /// <summary>
    /// Completed results for a campaign, including inactive and past campaigns: everyone who finished, ranked.
    /// <c>limit</c> keeps only the first places (the result screen shows five).
    /// </summary>
    [HttpGet("{campaignId:int}/leaderboard")]
    [ProducesResponseType(typeof(LeaderboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LeaderboardDto>> Leaderboard(
        int campaignId,
        [FromQuery, Range(1, int.MaxValue)] int? limit = null,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _leaderboard.GetAsync(campaignId, limit, ct));
        }
        catch (CampaignException ex)
        {
            return CampaignProblem(ex);
        }
    }

    private ObjectResult CampaignProblem(CampaignException ex)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(
            HttpContext, statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
        problem.Extensions["code"] = ex.Code;
        return StatusCode(ex.StatusCode, problem);
    }
}
