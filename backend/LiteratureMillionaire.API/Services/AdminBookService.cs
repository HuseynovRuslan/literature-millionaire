using System.Globalization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Question banks in the admin panel (docs/admin-panel-plan.md, phase 5) - the table is called Books for
/// historical reasons, but only one kind of row in it is a book.
///
/// Every question belongs to one of these, and a campaign is built around one: "Ayın Kitabı" is played from
/// the month's book (title, author, cover), while "Yaşıl Bakı", "Bilik yarışı" and the rest are collections
/// with no author at all. The panel calls them banks for that reason, and only the title is required.
///
/// Nothing is deleted here. A book carries questions and campaigns, and past results are read through them;
/// a book that should no longer be offered is switched off instead, which takes its campaigns out of play
/// (see CampaignService) without touching a single result.
/// </summary>
public interface IAdminBookService
{
    Task<IReadOnlyList<AdminBookDto>> ListAsync(CancellationToken ct = default);

    /// <exception cref="AdminBookValidationException">When the book cannot be saved as sent.</exception>
    Task<AdminBookDto> CreateAsync(AdminBookInput input, AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminBookValidationException">When the book cannot be saved as sent.</exception>
    /// <returns>Null when there is no such book.</returns>
    Task<AdminBookDto?> UpdateAsync(int id, AdminBookInput input, AdminActor actor, CancellationToken ct = default);
}

/// <summary>Field → messages, in Azerbaijani, ready for the form.</summary>
public sealed class AdminBookValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The book cannot be saved.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AdminBookService : IAdminBookService
{
    public const int TitleMaxLength = 200;
    public const int AuthorMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;

    public AdminBookService(ApplicationDbContext db, IAdminAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AdminBookDto>> ListAsync(CancellationToken ct = default)
    {
        var books = await _db.Books.AsNoTracking().OrderBy(b => b.Title).ThenBy(b => b.Id).ToListAsync(ct);
        return await ToDtosAsync(books, ct);
    }

    public async Task<AdminBookDto> CreateAsync(AdminBookInput input, AdminActor actor, CancellationToken ct = default)
    {
        var errors = await ValidateAsync(input, existing: null, ct);
        ThrowIfAny(errors);

        var book = new Book();
        Apply(book, input);
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.Books.Add(book);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "book-created", "book", book.Id.ToString(CultureInfo.InvariantCulture),
            $"{book.Title} — {book.Author}{(book.IsActive ? string.Empty : " · deaktiv")}", ct);
        await transaction.CommitAsync(ct);

        return (await ToDtosAsync([book], ct))[0];
    }

    public async Task<AdminBookDto?> UpdateAsync(int id, AdminBookInput input, AdminActor actor, CancellationToken ct = default)
    {
        var book = await _db.Books.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (book is null)
        {
            return null;
        }

        var errors = await ValidateAsync(input, book, ct);
        ThrowIfAny(errors);

        var before = Snapshot(book);
        Apply(book, input);
        var changes = Changes(before, Snapshot(book));
        if (changes.Count == 0)
        {
            return (await ToDtosAsync([book], ct))[0];
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "book-updated", "book", book.Id.ToString(CultureInfo.InvariantCulture),
            string.Join("; ", changes), ct);
        await transaction.CommitAsync(ct);

        return (await ToDtosAsync([book], ct))[0];
    }

    // --- rules ------------------------------------------------------------------------------------------------

    private async Task<Dictionary<string, List<string>>> ValidateAsync(AdminBookInput input, Book? existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, List<string>>();

        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) Add(errors, "title", "Kitabın adını yazın.");
        else if (title.Length > TitleMaxLength) Add(errors, "title", $"Ad ən çox {TitleMaxLength} simvol ola bilər.");
        else if (await _db.Books.AnyAsync(b => b.Id != (existing != null ? existing.Id : 0) && b.Title == title, ct))
        {
            // Two books with the same title would be told apart only by an id nobody sees: the campaign form,
            // the question list and the results all show the title.
            Add(errors, "title", "Bu adla kitab artıq var.");
        }

        // Optional on purpose: only one of these banks is a book. "Yaşıl Bakı" is a collection of
        // photographs and has no author, and demanding one would have people typing a company name in
        // to get past the form - which is exactly what the seeded rows already show.
        var author = input.Author?.Trim() ?? string.Empty;
        if (author.Length > AuthorMaxLength) Add(errors, "author", $"Müəllif ən çox {AuthorMaxLength} simvol ola bilər.");

        var description = input.Description?.Trim() ?? string.Empty;
        if (description.Length > DescriptionMaxLength)
        {
            Add(errors, "description", $"Təsvir ən çox {DescriptionMaxLength} simvol ola bilər.");
        }

        if (!LocalImagePath.IsCover(input.CoverImageUrl))
        {
            Add(errors, "coverImageUrl", "Üz qabığını Şəkillər bölməsindən seçin: kənar ünvan qəbul olunmur.");
        }

        // Switching off a book takes its campaigns out of play, so the panel says so before it happens rather
        // than leaving somebody to find the category gone from the home page.
        if (existing is not null && existing.IsActive && !input.IsActive)
        {
            var playing = await _db.MonthlyCampaigns
                .Where(c => c.BookId == existing.Id && c.IsEnabled && c.EndDate >= CampaignCalendar.Today())
                .Select(c => c.Id)
                .ToListAsync(ct);
            if (playing.Count > 0)
            {
                Add(errors, "isActive",
                    $"Bu kitab hazırda oynanılan kampaniyadadır (#{string.Join(", #", playing)}). Əvvəlcə həmin kampaniyanı deaktiv edin.");
            }
        }

        return errors;
    }

    // --- data -------------------------------------------------------------------------------------------------

    private async Task<List<AdminBookDto>> ToDtosAsync(IReadOnlyList<Book> books, CancellationToken ct)
    {
        var ids = books.Select(b => b.Id).ToList();
        var questions = await _db.Questions.AsNoTracking()
            .Where(q => q.BookId != null && ids.Contains(q.BookId.Value))
            .GroupBy(q => q.BookId!.Value)
            .Select(g => new { BookId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BookId, x => x.Count, ct);

        var campaigns = await _db.MonthlyCampaigns.AsNoTracking()
            .Where(c => c.BookId != null && ids.Contains(c.BookId.Value))
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.Id)
            .Select(c => new { BookId = c.BookId!.Value, Row = new AdminBookCampaignDto(c.Id, c.QuizMode.Title, c.StartDate, c.EndDate, c.IsEnabled) })
            .ToListAsync(ct);

        return books.Select(b =>
        {
            var own = campaigns.Where(c => c.BookId == b.Id).Select(c => c.Row).ToList();
            return new AdminBookDto(b.Id, b.Title, b.Author, b.Description, b.CoverImageUrl, b.IsActive,
                questions.TryGetValue(b.Id, out var count) ? count : 0, own.Count, own);
        }).ToList();
    }

    private static void Apply(Book book, AdminBookInput input)
    {
        book.Title = input.Title!.Trim();
        book.Author = input.Author?.Trim() ?? string.Empty;
        book.Description = input.Description?.Trim() ?? string.Empty;
        book.CoverImageUrl = input.CoverImageUrl?.Trim() ?? string.Empty;
        book.IsActive = input.IsActive;
    }

    private sealed record BookSnapshot(string Title, string Author, string Description, string CoverImageUrl, bool IsActive);

    private static BookSnapshot Snapshot(Book b) => new(b.Title, b.Author, b.Description, b.CoverImageUrl, b.IsActive);

    /// <summary>What an edit changed, in words, for the audit trail.</summary>
    private static List<string> Changes(BookSnapshot before, BookSnapshot after)
    {
        var changes = new List<string>();
        if (before.Title != after.Title) changes.Add($"Ad: \"{before.Title}\" → \"{after.Title}\"");
        if (before.Author != after.Author) changes.Add($"Müəllif: \"{before.Author}\" → \"{after.Author}\"");
        if (before.Description != after.Description) changes.Add("Təsvir dəyişdi");
        if (before.CoverImageUrl != after.CoverImageUrl)
        {
            changes.Add($"Üz qabığı: {Show(before.CoverImageUrl)} → {Show(after.CoverImageUrl)}");
        }
        if (before.IsActive != after.IsActive) changes.Add($"Aktiv: {YesNo(before.IsActive)} → {YesNo(after.IsActive)}");
        return changes;
    }

    private static string Show(string coverImageUrl) => coverImageUrl.Length == 0 ? "yoxdur" : coverImageUrl;

    private static string YesNo(bool value) => value ? "bəli" : "xeyr";

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.Add(message);
    }

    private static void ThrowIfAny(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new AdminBookValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
