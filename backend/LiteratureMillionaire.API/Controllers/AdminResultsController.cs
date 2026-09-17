using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Results, the prize export, and the corrections that remove attempts or participants.</summary>
[ApiController]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminResultsController : ControllerBase
{
    private readonly IAdminResultsService _results;

    public AdminResultsController(IAdminResultsService results) => _results = results;

    [HttpGet("api/admin/campaigns/{campaignId:int}/results")]
    [ProducesResponseType(typeof(AdminCampaignResultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Results(int campaignId, [FromQuery] string? search, CancellationToken ct) =>
        await _results.GetAsync(campaignId, search, ct) is { } results ? Ok(results) : NotFound();

    /// <summary>The Excel list, with full phone numbers. Recorded in the audit trail.</summary>
    [HttpGet("api/admin/campaigns/{campaignId:int}/results.xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task<IActionResult> Export(int campaignId, CancellationToken ct)
    {
        if (await _results.ExportAsync(campaignId, AdminAuth.Actor(User), ct) is not { } export)
        {
            return NotFound();
        }

        // Personal data: never kept by a browser or proxy cache.
        Response.Headers.CacheControl = "no-store";
        return File(export.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.FileName);
    }

    /// <summary>Deletes one attempt, so that person can play the campaign again.</summary>
    [HttpPost("api/admin/attempts/{attemptId:int}/reset")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> ResetAttempt(int attemptId, [FromBody] AdminReasonInput input, CancellationToken ct) =>
        Run(() => _results.ResetAttemptAsync(attemptId, input.Reason, AdminAuth.Actor(User), ct));

    /// <summary>Deletes a participant and all their attempts in every campaign (test entries).</summary>
    [HttpPost("api/admin/participants/{participantId:int}/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<IActionResult> RemoveParticipant(int participantId, [FromBody] AdminReasonInput input, CancellationToken ct) =>
        Run(() => _results.RemoveParticipantAsync(participantId, input.Reason, AdminAuth.Actor(User), ct));

    private async Task<IActionResult> Run(Func<Task> action)
    {
        try
        {
            await action();
            return NoContent();
        }
        catch (AdminResultsException ex)
        {
            var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, ex.StatusCode, detail: ex.Message);
            problem.Extensions["code"] = ex.Code;
            return StatusCode(ex.StatusCode, problem);
        }
    }
}
