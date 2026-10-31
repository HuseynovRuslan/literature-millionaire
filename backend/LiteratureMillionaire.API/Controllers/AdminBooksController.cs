using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Books in the admin panel. Every change is recorded in the audit trail by the service.</summary>
[ApiController]
[Route("api/admin/books")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminBooksController : ControllerBase
{
    private readonly IAdminBookService _books;

    public AdminBooksController(IAdminBookService books) => _books = books;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminBookDto>>> List(CancellationToken ct) =>
        Ok(await _books.ListAsync(ct));

    [HttpPost]
    [ProducesResponseType(typeof(AdminBookDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] AdminBookInput input, CancellationToken ct)
    {
        try
        {
            return StatusCode(StatusCodes.Status201Created, await _books.CreateAsync(input, AdminAuth.Actor(User), ct));
        }
        catch (AdminBookValidationException ex)
        {
            return Invalid(ex);
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AdminBookDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] AdminBookInput input, CancellationToken ct)
    {
        try
        {
            var updated = await _books.UpdateAsync(id, input, AdminAuth.Actor(User), ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (AdminBookValidationException ex)
        {
            return Invalid(ex);
        }
    }

    private ObjectResult Invalid(AdminBookValidationException ex)
    {
        var problem = new ValidationProblemDetails(ex.Errors.ToDictionary(e => e.Key, e => e.Value))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Kitab yadda saxlanmadı.",
        };
        problem.Extensions["code"] = "BOOK_INVALID";
        return BadRequest(problem);
    }
}
