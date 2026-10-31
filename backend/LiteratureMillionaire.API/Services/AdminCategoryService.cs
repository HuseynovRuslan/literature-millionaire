using System.Globalization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Categories in the admin panel (docs/admin-panel-plan.md, phase 8): the tiles on the home page - their title,
/// description, icon, order, whether they are offered at all, and whether they carry the "test version" label.
///
/// Editing only. A category is a quiz mode, and a new one needs a slug that questions, campaigns and the card
/// artwork all key on - adding one is a change to the product, not a form. Nothing is deleted either: a
/// category carries campaigns and every result played in them, so it is switched off instead.
/// </summary>
public interface IAdminCategoryService
{
    Task<IReadOnlyList<AdminCategoryDto>> ListAsync(CancellationToken ct = default);

    /// <exception cref="AdminCategoryValidationException">When the category cannot be saved as sent.</exception>
    /// <returns>Null when there is no such category.</returns>
    Task<AdminCategoryDto?> UpdateAsync(int id, AdminCategoryInput input, AdminActor actor, CancellationToken ct = default);

    /// <summary>Swaps a category with the one above or below it, so the home page can be ordered by eye.</summary>
    /// <returns>Null when there is no such category; the unchanged list when it is already at the end.</returns>
    Task<IReadOnlyList<AdminCategoryDto>?> MoveAsync(int id, bool up, AdminActor actor, CancellationToken ct = default);
}

/// <summary>Field → messages, in Azerbaijani, ready for the form.</summary>
public sealed class AdminCategoryValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The category cannot be saved.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class AdminCategoryService : IAdminCategoryService
{
    private readonly ApplicationDbContext _db;
    private readonly IAdminAuditLog _audit;

    public AdminCategoryService(ApplicationDbContext db, IAdminAuditLog audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> ListAsync(CancellationToken ct = default)
    {
        var modes = await _db.QuizModes.AsNoTracking().OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToListAsync(ct);
        return await ToDtosAsync(modes, ct);
    }

    public async Task<AdminCategoryDto?> UpdateAsync(int id, AdminCategoryInput input, AdminActor actor, CancellationToken ct = default)
    {
        var mode = await _db.QuizModes.FirstOrDefaultAsync(m => m.Id == id, ct);
        if (mode is null)
        {
            return null;
        }

        var errors = await ValidateAsync(input, mode, ct);
        if (errors.Count > 0)
        {
            throw new AdminCategoryValidationException(errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }

        var before = Snapshot(mode);
        mode.Title = input.Title!.Trim();
        mode.Description = input.Description?.Trim() ?? string.Empty;
        mode.IconKey = input.IconKey!.Trim();
        mode.IsActive = input.IsActive;
        mode.IsPreview = input.IsPreview;

        var changes = Changes(before, Snapshot(mode));
        if (changes.Count == 0)
        {
            return (await ToDtosAsync([mode], ct))[0];
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "category-updated", "category", id.ToString(CultureInfo.InvariantCulture),
            $"{mode.Title}: {string.Join("; ", changes)}", ct);
        await transaction.CommitAsync(ct);

        return (await ToDtosAsync([mode], ct))[0];
    }

    public async Task<IReadOnlyList<AdminCategoryDto>?> MoveAsync(int id, bool up, AdminActor actor, CancellationToken ct = default)
    {
        var modes = await _db.QuizModes.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).ToListAsync(ct);
        var index = modes.FindIndex(m => m.Id == id);
        if (index < 0)
        {
            return null;
        }

        var swapWith = up ? index - 1 : index + 1;
        if (swapWith < 0 || swapWith >= modes.Count)
        {
            return await ToDtosAsync(modes, ct); // already first or last: nothing to do, and nothing to say about it
        }

        // Renumbered from one on every move: the orders in the database drifted apart over the years (gaps,
        // duplicates), and a swap that only exchanges two numbers keeps the drift. This makes the list say
        // exactly what the home page shows.
        (modes[index], modes[swapWith]) = (modes[swapWith], modes[index]);
        for (var i = 0; i < modes.Count; i++)
        {
            modes[i].DisplayOrder = i + 1;
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.SaveChangesAsync(ct);
        await _audit.RecordAsync(actor, "category-updated", "category", id.ToString(CultureInfo.InvariantCulture),
            $"{modes[swapWith].Title}: sıra dəyişdi → {string.Join(", ", modes.Select(m => m.Title))}", ct);
        await transaction.CommitAsync(ct);

        return await ToDtosAsync(modes, ct);
    }

    // --- rules ------------------------------------------------------------------------------------------------

    private async Task<Dictionary<string, List<string>>> ValidateAsync(AdminCategoryInput input, QuizMode existing, CancellationToken ct)
    {
        var errors = new Dictionary<string, List<string>>();

        var title = input.Title?.Trim() ?? string.Empty;
        if (title.Length == 0) QuestionContentRules.Add(errors, "title", "Kateqoriyanın adını yazın.");
        else if (title.Length > QuizMode.TitleMaxLength) QuestionContentRules.Add(errors, "title", $"Ad ən çox {QuizMode.TitleMaxLength} simvol ola bilər.");
        else if (await _db.QuizModes.AnyAsync(m => m.Id != existing.Id && m.Title == title, ct))
        {
            QuestionContentRules.Add(errors, "title", "Bu adla kateqoriya artıq var.");
        }

        if ((input.Description?.Trim().Length ?? 0) > QuizMode.DescriptionMaxLength)
        {
            QuestionContentRules.Add(errors, "description", $"Təsvir ən çox {QuizMode.DescriptionMaxLength} simvol ola bilər.");
        }

        // The icon is drawn locally by the frontend; a key it does not know renders as a generic glyph.
        if (!QuizMode.IconKeys.Contains(input.IconKey?.Trim() ?? string.Empty, StringComparer.Ordinal))
        {
            QuestionContentRules.Add(errors, "iconKey", "Nişanı siyahıdan seçin.");
        }

        // Switching a category off takes its campaigns off the home page, so the panel says which one first.
        if (existing.IsActive && !input.IsActive)
        {
            var playing = await _db.MonthlyCampaigns
                .Where(c => c.QuizModeId == existing.Id && c.IsEnabled && c.EndDate >= CampaignCalendar.Today())
                .Select(c => c.Id)
                .ToListAsync(ct);
            if (playing.Count > 0)
            {
                QuestionContentRules.Add(errors, "isActive",
                    $"Bu kateqoriyanın oynanılan kampaniyası var (#{string.Join(", #", playing)}). Əvvəlcə həmin kampaniyanı deaktiv edin.");
            }
        }

        return errors;
    }

    // --- data -------------------------------------------------------------------------------------------------

    private async Task<List<AdminCategoryDto>> ToDtosAsync(IReadOnlyList<QuizMode> modes, CancellationToken ct)
    {
        var ids = modes.Select(m => m.Id).ToList();
        var questions = await _db.Questions.AsNoTracking()
            .Where(q => q.QuizModeId != null && ids.Contains(q.QuizModeId.Value))
            .GroupBy(q => q.QuizModeId!.Value)
            .Select(g => new { ModeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ModeId, x => x.Count, ct);

        var today = CampaignCalendar.Today();
        var campaigns = await _db.MonthlyCampaigns.AsNoTracking()
            .Where(c => ids.Contains(c.QuizModeId))
            .Select(c => new { c.QuizModeId, Running = c.IsEnabled && c.StartDate <= today && c.EndDate >= today })
            .ToListAsync(ct);

        return modes.Select(m => new AdminCategoryDto(
            m.Id, m.Slug, m.Title, m.Description, m.IconKey, m.DisplayOrder, m.IsActive, m.IsPreview,
            QuizModeSlugs.RequiresBook(m.Slug),
            questions.TryGetValue(m.Id, out var count) ? count : 0,
            campaigns.Count(c => c.QuizModeId == m.Id),
            campaigns.Any(c => c.QuizModeId == m.Id && c.Running))).ToList();
    }

    private sealed record CategorySnapshot(string Title, string Description, string IconKey, bool IsActive, bool IsPreview);

    private static CategorySnapshot Snapshot(QuizMode m) => new(m.Title, m.Description, m.IconKey, m.IsActive, m.IsPreview);

    private static List<string> Changes(CategorySnapshot before, CategorySnapshot after)
    {
        var changes = new List<string>();
        if (before.Title != after.Title) changes.Add($"Ad: \"{before.Title}\" → \"{after.Title}\"");
        if (before.Description != after.Description) changes.Add("Təsvir dəyişdi");
        if (before.IconKey != after.IconKey) changes.Add($"Nişan: {before.IconKey} → {after.IconKey}");
        if (before.IsActive != after.IsActive) changes.Add($"Aktiv: {YesNo(before.IsActive)} → {YesNo(after.IsActive)}");
        if (before.IsPreview != after.IsPreview) changes.Add($"Test versiya: {YesNo(before.IsPreview)} → {YesNo(after.IsPreview)}");
        return changes;
    }

    private static string YesNo(bool value) => value ? "bəli" : "xeyr";
}
