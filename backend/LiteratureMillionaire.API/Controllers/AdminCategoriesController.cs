using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Categories in the admin panel. Every change is recorded in the audit trail by the service.</summary>
[ApiController]
[Route("api/admin/categories")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminCategoriesController : ControllerBase
{
    private readonly IAdminCategoryService _categories;

    public AdminCategoriesController(IAdminCategoryService categories) => _categories = categories;

    public sealed record CategoryOptionsDto(IReadOnlyList<string> IconKeys);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCategoryDto>>> List(CancellationToken ct) =>
        Ok(await _categories.ListAsync(ct));

    /// <summary>The icons the frontend can draw. Anything else would render as a generic glyph.</summary>
    [HttpGet("options")]
    public ActionResult<CategoryOptionsDto> Options() => Ok(new CategoryOptionsDto(QuizMode.IconKeys));

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminCategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AdminCategoryInput input, CancellationToken ct)
    {
        try
        {
            var updated = await _categories.UpdateAsync(id, input, AdminAuth.Actor(User), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AdminCategoryValidationException ex)
        {
            var problem = new ValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Kateqoriya yadda saxlanmadı.",
            };
            problem.Extensions["code"] = "CATEGORY_INVALID";
            return BadRequest(problem);
        }
    }

    /// <summary>Moves a category one place up or down the home page.</summary>
    [HttpPost("{id:int}/move")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Move(int id, [FromQuery] string direction, CancellationToken ct)
    {
        var moved = await _categories.MoveAsync(id, direction != "down", AdminAuth.Actor(User), ct);
        return moved is null ? NotFound() : Ok(moved);
    }
}
