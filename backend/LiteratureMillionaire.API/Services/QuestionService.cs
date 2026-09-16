using System.Linq.Expressions;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

public class QuestionService : IQuestionService
{
    private readonly ApplicationDbContext _db;

    public QuestionService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<QuestionDto>> GetAllAsync(Difficulty? difficulty, string? category, int? bookId, CancellationToken ct = default)
    {
        IQueryable<Question> query = _db.Questions.AsNoTracking();

        if (difficulty.HasValue)
        {
            query = query.Where(q => q.Difficulty == difficulty.Value);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var trimmed = category.Trim();
            query = query.Where(q => q.Category == trimmed);
        }

        if (bookId.HasValue)
        {
            query = query.Where(q => q.BookId == bookId.Value);
        }

        return await query
            .OrderBy(q => q.Difficulty)
            .ThenBy(q => q.Id)
            .Select(ToDtoExpression)
            .ToListAsync(ct);
    }

    public async Task<QuestionDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _db.Questions.AsNoTracking()
            .Where(q => q.Id == id)
            .Select(ToDtoExpression)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<QuestionDto> CreateAsync(CreateQuestionDto dto, CancellationToken ct = default)
    {
        await EnsureBookExistsAsync(dto.BookId, ct);

        var entity = new Question { CreatedAt = DateTime.UtcNow };
        Apply(entity, dto);
        // A new question is played in the quiz mode of its book's campaigns.
        entity.QuizModeId = await QuizModeForBookAsync(entity.BookId!.Value, ct);

        _db.Questions.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(entity.Id, ct))!;
    }

    public async Task<QuestionDto?> UpdateAsync(int id, UpdateQuestionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null)
        {
            return null;
        }

        await EnsureBookExistsAsync(dto.BookId, ct);

        var bookChanged = entity.BookId != dto.BookId;
        Apply(entity, dto);
        // An assigned mode is kept; it is (re)derived only when the question has none or moves to another book.
        if (bookChanged || entity.QuizModeId is null)
        {
            entity.QuizModeId = await QuizModeForBookAsync(entity.BookId!.Value, ct);
        }
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(entity.Id, ct))!;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (entity is null)
        {
            return false;
        }

        _db.Questions.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static void Apply(Question entity, CreateQuestionDto dto)
    {
        entity.Text = dto.Text.Trim();
        entity.OptionA = dto.OptionA.Trim();
        entity.OptionB = dto.OptionB.Trim();
        entity.OptionC = dto.OptionC.Trim();
        entity.OptionD = dto.OptionD.Trim();
        entity.CorrectOption = dto.CorrectOption.Trim()[0];
        entity.Difficulty = dto.Difficulty!.Value; // validated as [Required] by the model binder
        entity.Category = dto.Category.Trim();
        entity.Explanation = string.IsNullOrWhiteSpace(dto.Explanation) ? null : dto.Explanation.Trim();
        entity.BookId = dto.BookId!.Value; // [Required] + existence checked in EnsureBookExistsAsync
        // Pairing and path rules are validated on the DTO (IValidatableObject); blanks mean "no image".
        entity.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim();
        entity.ImageAltText = string.IsNullOrWhiteSpace(dto.ImageAltText) ? null : dto.ImageAltText.Trim();
    }

    /// <summary>Quiz mode of the book's most recent campaign; null when the book has no campaign yet (the question is then not played).</summary>
    private Task<int?> QuizModeForBookAsync(int bookId, CancellationToken ct) =>
        _db.MonthlyCampaigns
            .AsNoTracking()
            .Where(c => c.BookId == bookId)
            .OrderByDescending(c => c.StartDate)
            .ThenByDescending(c => c.Id)
            .Select(c => (int?)c.QuizModeId)
            .FirstOrDefaultAsync(ct);

    /// <summary>The book may be inactive (questions can be prepared ahead), but it must exist.</summary>
    private async Task EnsureBookExistsAsync(int? bookId, CancellationToken ct)
    {
        if (bookId is null || bookId <= 0 || !await _db.Books.AnyAsync(b => b.Id == bookId.Value, ct))
        {
            throw new QuestionValidationException(nameof(CreateQuestionDto.BookId), $"Book {bookId} does not exist.");
        }
    }

    private static readonly Expression<Func<Question, QuestionDto>> ToDtoExpression = q => new QuestionDto(
        q.Id,
        q.Text,
        q.OptionA,
        q.OptionB,
        q.OptionC,
        q.OptionD,
        q.CorrectOption.ToString(),
        q.Difficulty,
        q.Category,
        q.Explanation,
        q.CreatedAt,
        q.BookId,
        q.Book != null ? q.Book.Title : null,
        q.ImageUrl,
        q.ImageAltText,
        q.QuizModeId,
        q.QuizMode != null ? q.QuizMode.Slug : null);
}
