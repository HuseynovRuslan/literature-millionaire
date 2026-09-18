using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// The quiz modes. A mode is created when its slug is missing; existing rows are never updated, so an
/// administrator's changes to titles, order or IsActive survive restarts. The AddQuizModes migration inserts the
/// same rows on databases upgraded from the single-campaign schema.
/// </summary>
public static class QuizModeSeed
{
    public sealed record Definition(string Slug, string Title, string Description, string IconKey, int DisplayOrder,
        bool IsPreview = false);

    public static readonly IReadOnlyList<Definition> Modes = new[]
    {
        new Definition(QuizModeSlugs.BilikDunyasi, "Bilik Dünyası",
            "Dünya bayraqları, paytaxtlar, Azərbaycan, ümumi biliklər və Azərbaycan kinosu üzrə qarışıq bilik yarışı.", "globe", 1),
        new Definition(QuizModeSlugs.AyinKitabi, "Ayın Kitabı",
            "Ayın seçilmiş kitabı üzrə bilik yarışı.", "book", 2),
        new Definition(QuizModeSlugs.EdebiyyatDunyasi, "Ədəbiyyat Dünyası",
            "Azərbaycan və dünya ədəbiyyatı üzrə bilik yarışı.", "feather", 3),
        new Definition(QuizModeSlugs.YasilBaki, "Yaşıl Bakı",
            "Bakının bitkiləri, parkları və yaşıllıqları üzrə bilik yarışı.", "leaf", 4),
        // Playable, not finished: the catalogue's names are trade names and half of them carry no sourced
        // Azerbaijani name yet, so the card says "test version" until the panel takes the flag off.
        new Definition(QuizModeSlugs.GreenGarden, "Green Garden Kolleksiyası",
            "Green Garden kataloqundakı bəzək bitkiləri: ağac, kol, sarmaşıq və onların sortları.", "sprout", 5,
            IsPreview: true),
    };

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var invalid = Modes.Where(m => !QuizMode.IsValidSlug(m.Slug)).Select(m => m.Slug).ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException($"Quiz mode seed has invalid slug(s): {string.Join(", ", invalid)}.");
        }

        var existing = (await db.QuizModes.Select(m => m.Slug).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
        var missing = Modes
            .Where(m => !existing.Contains(m.Slug))
            .Select(m => new QuizMode
            {
                Slug = m.Slug,
                Title = m.Title,
                Description = m.Description,
                IconKey = m.IconKey,
                DisplayOrder = m.DisplayOrder,
                IsPreview = m.IsPreview,
                IsActive = true
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.QuizModes.AddRange(missing);
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Id of the mode with this slug. Seed the modes first.</summary>
    public static async Task<int> GetIdAsync(ApplicationDbContext db, string slug, CancellationToken ct = default) =>
        await db.QuizModes.Where(m => m.Slug == slug).Select(m => (int?)m.Id).FirstOrDefaultAsync(ct)
        ?? throw new InvalidOperationException($"Quiz mode '{slug}' was not found. Seed quiz modes before campaigns and questions.");
}
