namespace LiteratureMillionaire.API.Dtos;

/// <summary>Lightweight book row for admin pickers and filters.</summary>
public record BookListItemDto(int Id, string Title, string Author, bool IsActive);
