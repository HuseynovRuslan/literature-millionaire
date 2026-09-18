using System.Globalization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// The question editor (docs/admin-panel-plan.md, phase 6). The panel owns the question banks from here on -
/// the seeders fill an empty bank once and never touch it again (see BankOwnership) - so this is the only
/// place a question is written.
///
/// The rules it enforces are the ones the generated banks were held to, because a question typed here is
/// played next to those: four options that are actually different, a correct one among them, and - for an
/// illustrated question - alt text that does not give the answer away and a credit that travels with the
/// picture. A licence is required with a picture because some of them are only ours to show on that
/// condition, and a credit nobody recorded is a credit nobody can restore later.
/// </summary>
public interface IAdminQuestionService
{
    Task<AdminQuestionPageDto> ListAsync(AdminQuestionFilter filter, CancellationToken ct = default);

    Task<AdminQuestionOptionsDto> OptionsAsync(CancellationToken ct = default);

    /// <exception cref="AdminQuestionValidationException">When the question cannot be saved as sent.</exception>
    Task<AdminQuestionDto> CreateAsync(AdminQuestionInput input, AdminActor actor, CancellationToken ct = default);

    /// <exception cref="AdminQuestionValidationException">When the question cannot be saved as sent.</exception>
    /// <returns>Null when there is no such question.</returns>
    Task<AdminQuestionDto?> UpdateAsync(int id, AdminQuestionInput input, AdminActor actor, CancellationToken ct = default);

    /// <returns>False when there is no such question.</returns>
    Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken ct = default);
}

/// <summary>What the editor is looking for. Everything is optional; nothing set means the whole bank.</summary>
public sealed record AdminQuestionFilter(
    int? BookId = null,
    int? QuizModeId = null,
    Difficulty? Difficulty = null,
    bool? WithImage = null,
    string? Search = null,
    int Skip = 0,
    int Take = 50);

/// <summary>Field → messages, in Azerbaijani, ready for the form.</summary>
public sealed class AdminQuestionValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The question cannot be saved.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AdminQuestionService : IAdminQuestionService
{
    public const int MaxTake = 200;

    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;

    public AdminQuestionService(ApplicationDbContext db, IAdminAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<AdminQuestionPageDto> ListAsync(AdminQuestionFilter filter, CancellationToken ct = default)
    {
        var query = _db.Questions.AsNoTracking().AsQueryable();

        if (filter.BookId is { } bookId) query = query.Where(q => q.BookId == bookId);
        if (filter.QuizModeId is { } modeId) query = query.Where(q => q.QuizModeId == modeId);
        if (filter.Difficulty is { } difficulty) query = query.Where(q => q.Difficulty == difficulty);
        if (filter.WithImage is { } withImage) query = query.Where(q => withImage ? q.ImageUrl != null : q.ImageUrl == null);

        var search = filter.Search?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            // The question, its options and its category: what somebody looking for "bayraq" would expect
            // to find it by. EF translates this to ILIKE on PostgreSQL.
            query = query.Where(q => EF.Functions.Like(q.Text, $"%{search}%")
                                     || EF.Functions.Like(q.OptionA, $"%{search}%")
                                     || EF.Functions.Like(q.OptionB, $"%{search}%")
                                     || EF.Functions.Like(q.OptionC, $"%{search}%")
                                     || EF.Functions.Like(q.OptionD, $"%{search}%")
                                     || EF.Functions.Like(q.Category, $"%{search}%"));
        }

        var total = await query.CountAsync(ct);
        var take = Math.Clamp(filter.Take, 1, MaxTake);
        var skip = Math.Max(0, filter.Skip);
        var rows = await query
            .OrderByDescending(q => q.Id)
            .Skip(skip)
            .Take(take)
            .Select(ToDtoExpression)
            .ToListAsync(ct);

        return new AdminQuestionPageDto(total, skip, take, rows);
    }

    public async Task<AdminQuestionOptionsDto> OptionsAsync(CancellationToken ct = default)
    {
        var counts = await _db.Questions.AsNoTracking()
            .Where(q => q.BookId != null)
            .GroupBy(q => new { BookId = q.BookId!.Value, q.Difficulty, HasImage = q.ImageUrl != null })
            .Select(g => new { g.Key.BookId, g.Key.Difficulty, g.Key.HasImage, Count = g.Count() })
            .ToListAsync(ct);

        var books = await _db.Books.AsNoTracking().OrderBy(b => b.Title).ToListAsync(ct);
        var banks = books.Select(b =>
        {
            var own = counts.Where(c => c.BookId == b.Id).ToList();
            int Of(Difficulty d) => own.Where(c => c.Difficulty == d).Sum(c => c.Count);
            return new AdminQuestionBankDto(b.Id, b.Title, b.IsActive, Of(Entities.Difficulty.Easy),
                Of(Entities.Difficulty.Medium), Of(Entities.Difficulty.Hard), own.Where(c => c.HasImage).Sum(c => c.Count));
        }).ToList();

        var modes = await _db.QuizModes.AsNoTracking().OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)
            .Select(m => new AdminQuizModeOptionDto(m.Id, m.Title, m.IsActive, QuizModeSlugs.RequiresBook(m.Slug)))
            .ToListAsync(ct);

        // The sub-categories already in use ("Bayraqlar", "Bitkilər", ...), so the form suggests rather than asks.
        var categories = await _db.Questions.AsNoTracking()
            .Select(q => q.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        return new AdminQuestionOptionsDto(banks, modes, categories.Where(c => !string.IsNullOrWhiteSpace(c)).ToList(),
            QuizRules.EasyPerQuiz, QuizRules.MediumPerQuiz, QuizRules.HardPerQuiz);
    }

    public async Task<AdminQuestionDto> CreateAsync(AdminQuestionInput input, AdminActor actor, CancellationToken ct = default)
    {
        var errors = await ValidateAsync(input, ct);
        ThrowIfAny(errors);

        var question = new Question { CreatedAt = DateTime.UtcNow };
        QuestionContentRules.Apply(question, input);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.Questions.Add(question);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "question-created", "question", question.Id.ToString(CultureInfo.InvariantCulture),
            Describe(question), ct);
        await transaction.CommitAsync(ct);

        return await LoadDtoAsync(question.Id, ct);
    }

    public async Task<AdminQuestionDto?> UpdateAsync(int id, AdminQuestionInput input, AdminActor actor, CancellationToken ct = default)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (question is null)
        {
            return null;
        }

        var errors = await ValidateAsync(input, ct);
        ThrowIfAny(errors);

        var before = Snapshot(question);
        QuestionContentRules.Apply(question, input);
        var changes = Changes(before, Snapshot(question));
        if (changes.Count == 0)
        {
            return await LoadDtoAsync(id, ct);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "question-updated", "question", id.ToString(CultureInfo.InvariantCulture),
            string.Join("; ", changes), ct);
        await transaction.CommitAsync(ct);

        return await LoadDtoAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(int id, AdminActor actor, CancellationToken ct = default)
    {
        var question = await _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);
        if (question is null)
        {
            return false;
        }

        // The trail keeps the whole question, not a reference to a row that no longer exists: "which question
        // did we take out, and what did it say" has to have an answer after the fact.
        var details = $"{Describe(question)} · {question.Text}";

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        _db.Questions.Remove(question);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "question-deleted", "question", id.ToString(CultureInfo.InvariantCulture), details, ct);
        await transaction.CommitAsync(ct);
        return true;
    }

    // --- rules ------------------------------------------------------------------------------------------------

    private async Task<Dictionary<string, List<string>>> ValidateAsync(AdminQuestionInput input, CancellationToken ct)
    {
        // The content rules are shared with the bulk import (QuestionContentRules), so a question that arrives
        // in a workbook is held to exactly what the editor would have demanded of it.
        var errors = QuestionContentRules.Validate(input);

        if (input.BookId is not { } bookId || !await _db.Books.AnyAsync(b => b.Id == bookId, ct))
        {
            QuestionContentRules.Add(errors, "bookId", "Sual bankını seçin.");
        }

        // A question with no mode is never played: the game draws by mode, and by book within it.
        if (input.QuizModeId is not { } modeId || !await _db.QuizModes.AnyAsync(m => m.Id == modeId, ct))
        {
            QuestionContentRules.Add(errors, "quizModeId", "Kateqoriyanı seçin.");
        }

        return errors;
    }

    // --- data -------------------------------------------------------------------------------------------------

    private async Task<AdminQuestionDto> LoadDtoAsync(int id, CancellationToken ct) =>
        await _db.Questions.AsNoTracking().Where(q => q.Id == id).Select(ToDtoExpression).SingleAsync(ct);

    /// <summary>One projection for the list and for a single row, so both always show the same fields.</summary>
    private static readonly System.Linq.Expressions.Expression<Func<Question, AdminQuestionDto>> ToDtoExpression = q => new AdminQuestionDto(
        q.Id, q.Text, q.OptionA, q.OptionB, q.OptionC, q.OptionD, q.CorrectOption.ToString(), q.Difficulty, q.Category,
        q.Explanation, q.BookId, q.Book != null ? q.Book.Title : null, q.QuizModeId, q.QuizMode != null ? q.QuizMode.Title : null,
        q.ImageUrl, q.ImageAltText, q.ImageSource, q.ImageLicense, q.CreatedAt);

    private sealed record QuestionSnapshot(string Text, string A, string B, string C, string D, char Correct,
        Difficulty Difficulty, string Category, string? Explanation, int? BookId, int? QuizModeId,
        string? ImageUrl, string? Alt, string? Source, string? Licence);

    private static QuestionSnapshot Snapshot(Question q) => new(q.Text, q.OptionA, q.OptionB, q.OptionC, q.OptionD,
        q.CorrectOption, q.Difficulty, q.Category, q.Explanation, q.BookId, q.QuizModeId, q.ImageUrl, q.ImageAltText,
        q.ImageSource, q.ImageLicense);

    /// <summary>What an edit changed, in words, for the audit trail.</summary>
    private static List<string> Changes(QuestionSnapshot before, QuestionSnapshot after)
    {
        var changes = new List<string>();
        void Compare<T>(string label, T old, T now)
        {
            if (!EqualityComparer<T>.Default.Equals(old, now)) changes.Add(label);
        }

        if (before.Text != after.Text) changes.Add($"Sual: \"{Short(before.Text)}\" → \"{Short(after.Text)}\"");
        Compare("Variant A", before.A, after.A);
        Compare("Variant B", before.B, after.B);
        Compare("Variant C", before.C, after.C);
        Compare("Variant D", before.D, after.D);
        if (before.Correct != after.Correct) changes.Add($"Düzgün cavab: {before.Correct} → {after.Correct}");
        if (before.Difficulty != after.Difficulty) changes.Add($"Çətinlik: {before.Difficulty} → {after.Difficulty}");
        if (before.Category != after.Category) changes.Add($"Alt kateqoriya: {before.Category} → {after.Category}");
        Compare("İzah", before.Explanation, after.Explanation);
        if (before.BookId != after.BookId) changes.Add($"Bank: #{before.BookId} → #{after.BookId}");
        if (before.QuizModeId != after.QuizModeId) changes.Add($"Kateqoriya: #{before.QuizModeId} → #{after.QuizModeId}");
        if (before.ImageUrl != after.ImageUrl) changes.Add($"Şəkil: {before.ImageUrl ?? "yoxdur"} → {after.ImageUrl ?? "yoxdur"}");
        Compare("Alt mətn", before.Alt, after.Alt);
        Compare("Mənbə", before.Source, after.Source);
        Compare("Lisenziya", before.Licence, after.Licence);
        return changes;
    }

    private static string Describe(Question q) =>
        $"{q.Difficulty} · {q.Category}{(q.ImageUrl is null ? string.Empty : " · şəkilli")}";

    private static string Short(string text) => text.Length <= 60 ? text : text[..60] + "…";

    private static void ThrowIfAny(Dictionary<string, List<string>> errors)
    {
        if (errors.Count > 0)
        {
            throw new AdminQuestionValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
