using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>The panel's question editor. Every change is recorded in the audit trail by the service.</summary>
[ApiController]
[Route("api/admin/questions")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminQuestionsController : ControllerBase
{
    private readonly IAdminQuestionService _questions;

    public AdminQuestionsController(IAdminQuestionService questions) => _questions = questions;

    [HttpGet]
    public async Task<ActionResult<AdminQuestionPageDto>> List(
        [FromQuery] int? bookId,
        [FromQuery] int? quizModeId,
        [FromQuery] Difficulty? difficulty,
        [FromQuery] bool? withImage,
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        Ok(await _questions.ListAsync(new AdminQuestionFilter(bookId, quizModeId, difficulty, withImage, search, skip, take), ct));

    /// <summary>Banks, categories and the sub-categories already in use, for the filters and the form.</summary>
    [HttpGet("options")]
    public async Task<ActionResult<AdminQuestionOptionsDto>> Options(CancellationToken ct) =>
        Ok(await _questions.OptionsAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(AdminQuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AdminQuestionInput input, CancellationToken ct)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created, await _questions.CreateAsync(input, AdminAuth.Actor(User), ct));
        }
        catch (AdminQuestionValidationException ex)
        {
            return Invalid(ex);
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AdminQuestionInput input, CancellationToken ct)
    {
        try
        {
            var updated = await _questions.UpdateAsync(id, input, AdminAuth.Actor(User), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AdminQuestionValidationException ex)
        {
            return Invalid(ex);
        }
    }

    /// <summary>Removes one question. The trail keeps what it said, so a deletion can be explained afterwards.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await _questions.DeleteAsync(id, AdminAuth.Actor(User), ct) ? NoContent() : NotFound();

    private ObjectResult Invalid(AdminQuestionValidationException ex)
    {
        var problem = new ValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Sual yadda saxlanmadı.",
        };
        problem.Extensions["code"] = "QUESTION_INVALID";
        return BadRequest(problem);
    }
}
