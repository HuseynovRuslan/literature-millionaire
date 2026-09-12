using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Read-only book lookup for the admin question form. Book CRUD comes later.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class BooksController : ControllerBase
{
    private readonly IBookService _books;

    public BooksController(IBookService books)
    {
        _books = books;
    }

    /// <summary>All books ordered by title, including inactive ones.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BookListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BookListItemDto>>> GetAll(CancellationToken ct)
    {
        return Ok(await _books.GetAllAsync(ct));
    }
}
