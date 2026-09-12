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

    public CampaignsController(ICampaignService campaigns)
    {
        _campaigns = campaigns;
    }

    /// <summary>The "Book of the Month" campaign running today, with its book.</summary>
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
            var problem = ProblemDetailsFactory.CreateProblemDetails(
                HttpContext, statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
            problem.Extensions["code"] = ex.Code;
            return StatusCode(ex.StatusCode, problem);
        }
    }
}
