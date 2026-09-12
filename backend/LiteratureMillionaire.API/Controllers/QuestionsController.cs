using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>
/// Admin CRUD for questions. Game-specific endpoints (random question by
/// difficulty, answer checking without leaking the answer) will be added later.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class QuestionsController : ControllerBase
{
    private readonly IQuestionService _questions;

    public QuestionsController(IQuestionService questions)
    {
        _questions = questions;
    }

    /// <summary>List questions, optionally filtered by difficulty, category and/or book.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<QuestionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<QuestionDto>>> GetAll(
        [FromQuery] Difficulty? difficulty,
        [FromQuery] string? category,
        [FromQuery] int? bookId,
        CancellationToken ct)
    {
        var items = await _questions.GetAllAsync(difficulty, category, bookId, ct);
        return Ok(items);
    }

    /// <summary>Get a single question by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuestionDto>> GetById(int id, CancellationToken ct)
    {
        var item = await _questions.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    /// <summary>Create a new question.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuestionDto>> Create([FromBody] CreateQuestionDto dto, CancellationToken ct)
    {
        try
        {
            var created = await _questions.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (QuestionValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    /// <summary>Update an existing question.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuestionDto>> Update(int id, [FromBody] UpdateQuestionDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await _questions.UpdateAsync(id, dto, ct);
            return updated is null ? NotFound() : Ok(updated);
        }
        catch (QuestionValidationException ex)
        {
            return ToValidationProblem(ex);
        }
    }

    /// <summary>Delete a question.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _questions.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    /// <summary>Same 400 shape as DataAnnotations failures, keyed by the offending field.</summary>
    private ActionResult ToValidationProblem(QuestionValidationException ex)
    {
        ModelState.AddModelError(ex.Field, ex.Message);
        return ValidationProblem(ModelState);
    }
}
