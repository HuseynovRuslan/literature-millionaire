using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

public class BookService : IBookService
{
    private readonly ApplicationDbContext _db;

    public BookService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<BookListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Books
            .AsNoTracking()
            .OrderBy(b => b.Title)
            .ThenBy(b => b.Id)
            .Select(b => new BookListItemDto(b.Id, b.Title, b.Author, b.IsActive))
            .ToListAsync(ct);
    }
}
