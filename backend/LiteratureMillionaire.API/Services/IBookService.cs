using LiteratureMillionaire.API.Dtos;

namespace LiteratureMillionaire.API.Services;

public interface IBookService
{
    /// <summary>All books (active and inactive), ordered by title, for admin pickers.</summary>
    Task<IReadOnlyList<BookListItemDto>> GetAllAsync(CancellationToken ct = default);
}
