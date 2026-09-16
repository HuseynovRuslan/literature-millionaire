using System.Text.Json;
using System.Text.Json.Serialization;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Seed;

/// <summary>
/// The reviewed plant-recognition catalogue: 39 plants and the 50 photographs that belong to them.
///
/// Source of truth: docs/plants-import/mapping-and-sources.csv, converted at implementation time by
/// tools/import/convert_plant_catalog.py into Seed/Data/plant-catalog.json (embedded resource). The CSV is
/// never read at runtime and is deliberately kept out of frontend/public, so it is not served to visitors.
///
/// Photographs numbered plant-040..plant-050 are extra views of plants that already exist: they are added
/// to that plant's image list and never create a new plant or a new answer option.
///
/// Idempotency: a plant is identified by its catalogue id, a photograph by its file name. Rows that are
/// already stored are left exactly as they are - so an administrator's edits (including approving a
/// "Şərti" plant) survive every restart - and only missing rows are inserted, inside one transaction.
/// </summary>
public static class PlantCatalogSeed
{
    public const int ExpectedPlantCount = 39;
    public const int ExpectedImageCount = 50;

    private const string ResourceName = "LiteratureMillionaire.API.Seed.Data.plant-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    /// <summary>One catalogue plant as stored in the embedded JSON.</summary>
    public sealed record CatalogPlant(
        string CatalogId,
        string Name,
        string ScientificName,
        PlantQuizStatus QuizStatus,
        string? BotanicalNote,
        IReadOnlyList<CatalogImage> Images);

    /// <summary>One photograph with the attribution its licence requires.</summary>
    public sealed record CatalogImage(
        string FileName,
        string ImageUrl,
        bool IsPrimary,
        string Source,
        string? SourceUrl,
        string? Author,
        string License,
        string? LicenseUrl,
        string? SpecimenSpecies,
        string? OriginalLabel);

    private sealed record CatalogFile(IReadOnlyList<CatalogPlant> Plants);

    /// <summary>Reads the embedded catalogue. Throws when it is missing or unreadable.</summary>
    public static IReadOnlyList<CatalogPlant> LoadCatalog()
    {
        using var stream = typeof(PlantCatalogSeed).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded plant catalogue '{ResourceName}' was not found.");
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Plant catalogue could not be read.");
        return file.Plants;
    }

    /// <summary>Problems that make the catalogue unusable. Empty when it is valid.</summary>
    public static IReadOnlyList<string> Validate(IReadOnlyList<CatalogPlant> plants)
    {
        var problems = new List<string>();

        if (plants.Count != ExpectedPlantCount)
        {
            problems.Add($"Expected {ExpectedPlantCount} plants, found {plants.Count}.");
        }

        var images = plants.SelectMany(p => p.Images).ToList();
        if (images.Count != ExpectedImageCount)
        {
            problems.Add($"Expected {ExpectedImageCount} images, found {images.Count}.");
        }

        foreach (var duplicate in plants.GroupBy(p => p.CatalogId, StringComparer.Ordinal).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate catalogue id {duplicate.Key}.");
        }

        // Two plants sharing an answer name would let one question show the same option twice.
        foreach (var duplicate in plants.GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate answer name '{duplicate.Key}'.");
        }

        foreach (var duplicate in images.GroupBy(i => i.FileName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            problems.Add($"Duplicate image file {duplicate.Key}.");
        }

        foreach (var plant in plants)
        {
            if (string.IsNullOrWhiteSpace(plant.CatalogId)) problems.Add("A plant has no catalogue id.");
            if (string.IsNullOrWhiteSpace(plant.Name)) problems.Add($"{plant.CatalogId}: answer name is empty.");
            if (string.IsNullOrWhiteSpace(plant.ScientificName)) problems.Add($"{plant.CatalogId}: scientific name is empty.");
            if (plant.Images.Count == 0) problems.Add($"{plant.CatalogId}: has no photograph.");
            if (plant.Images.Count(i => i.IsPrimary) != 1) problems.Add($"{plant.CatalogId}: needs exactly one primary photograph.");

            foreach (var image in plant.Images)
            {
                if (!image.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    problems.Add($"{image.FileName}: only .webp files are accepted.");
                }
                if (image.ImageUrl != "/images/plants/" + image.FileName)
                {
                    problems.Add($"{image.FileName}: image url '{image.ImageUrl}' does not match the file name.");
                }
                // Attribution is a licence obligation, so a photograph without a source or licence is not usable.
                if (string.IsNullOrWhiteSpace(image.Source)) problems.Add($"{image.FileName}: source is empty.");
                if (string.IsNullOrWhiteSpace(image.License)) problems.Add($"{image.FileName}: licence is empty.");
            }
        }

        return problems;
    }

    public static async Task SeedAsync(ApplicationDbContext db, CancellationToken ct = default)
    {
        var catalog = LoadCatalog();
        var problems = Validate(catalog);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Plant catalogue is invalid:\n" + string.Join("\n", problems));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var existingPlants = await db.Plants
            .Select(p => new { p.Id, p.CatalogId })
            .ToDictionaryAsync(p => p.CatalogId, p => p.Id, StringComparer.Ordinal, ct);
        var existingImages = (await db.PlantImages.Select(i => i.FileName).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;

        // New plants first, so their photographs can reference them.
        var newPlants = catalog
            .Where(p => !existingPlants.ContainsKey(p.CatalogId))
            .Select(p => new Plant
            {
                CatalogId = p.CatalogId,
                Name = p.Name.Trim(),
                ScientificName = p.ScientificName.Trim(),
                QuizStatus = p.QuizStatus,
                BotanicalNote = string.IsNullOrWhiteSpace(p.BotanicalNote) ? null : p.BotanicalNote.Trim(),
                CreatedAt = now,
            })
            .ToList();

        if (newPlants.Count > 0)
        {
            db.Plants.AddRange(newPlants);
            await db.SaveChangesAsync(ct);
            foreach (var plant in newPlants)
            {
                existingPlants[plant.CatalogId] = plant.Id;
            }
        }

        // Then the missing photographs, in catalogue order (primary first).
        var newImages = new List<PlantImage>();
        foreach (var plant in catalog)
        {
            var plantId = existingPlants[plant.CatalogId];
            var order = 0;
            foreach (var image in plant.Images.OrderByDescending(i => i.IsPrimary))
            {
                var displayOrder = order++;
                if (existingImages.Contains(image.FileName))
                {
                    continue;
                }

                newImages.Add(new PlantImage
                {
                    PlantId = plantId,
                    FileName = image.FileName,
                    ImageUrl = image.ImageUrl,
                    IsPrimary = image.IsPrimary,
                    DisplayOrder = displayOrder,
                    Source = image.Source.Trim(),
                    SourceUrl = Clean(image.SourceUrl),
                    Author = Clean(image.Author),
                    License = image.License.Trim(),
                    LicenseUrl = Clean(image.LicenseUrl),
                    SpecimenSpecies = Clean(image.SpecimenSpecies),
                    OriginalLabel = Clean(image.OriginalLabel),
                    CreatedAt = now,
                });
            }
        }

        if (newImages.Count > 0)
        {
            db.PlantImages.AddRange(newImages);
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
