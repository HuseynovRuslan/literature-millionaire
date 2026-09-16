using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>The reviewed plant catalogue: what is imported, what stays out of the live quiz, and how a round is built.</summary>
public class PlantCatalogTests
{
    // --- the reviewed data file ----------------------------------------------

    [Fact]
    public void Catalogue_has_the_approved_counts_and_is_valid()
    {
        var plants = PlantCatalogSeed.LoadCatalog();

        Assert.Empty(PlantCatalogSeed.Validate(plants));
        Assert.Equal(39, plants.Count);
        Assert.Equal(39, PlantCatalogSeed.ExpectedPlantCount);
        Assert.Equal(50, plants.Sum(p => p.Images.Count));
        Assert.Equal(50, PlantCatalogSeed.ExpectedImageCount);
        // One primary photograph each; the remaining eleven are extra views of plants that already exist.
        Assert.Equal(39, plants.Count(p => p.Images.Count(i => i.IsPrimary) == 1));
        Assert.Equal(11, plants.Sum(p => p.Images.Count(i => !i.IsPrimary)));
        Assert.Equal(11, plants.Count(p => p.Images.Count > 1));
    }

    [Fact]
    public void Catalogue_keeps_the_reviewed_answer_names()
    {
        var names = PlantCatalogSeed.LoadCatalog().Select(p => p.Name).ToList();

        // Names the review asked to keep exactly as they are.
        Assert.Contains("Microphyllus", names);
        Assert.Contains("Passifloraceae", names);
        Assert.Contains("Geran (Pelargonium)", names);
        // "Geran (Pelargonium)" is one plant, not two.
        Assert.Single(names, n => n.Contains("Pelargonium", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("Pelargonium", names.Where(n => n != "Geran (Pelargonium)"));
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_catalogue_image_points_at_a_file_that_ships_with_the_frontend()
    {
        var images = PlantCatalogSeed.LoadCatalog().SelectMany(p => p.Images).ToList();
        var folder = Path.Combine(RepositoryRoot(), "frontend", "public", "images", "plants");

        Assert.All(images, i => Assert.Matches(@"^/images/plants/plant-\d{3}\.webp$", i.ImageUrl));
        Assert.All(images, i => Assert.True(File.Exists(Path.Combine(folder, i.FileName)), $"missing file: {i.FileName}"));
        // Nothing extra is published either: the folder holds exactly the catalogue's fifty photographs.
        Assert.Equal(50, Directory.GetFiles(folder, "*.webp").Length);
    }

    [Fact]
    public void Every_photograph_carries_the_attribution_its_licence_needs()
    {
        var images = PlantCatalogSeed.LoadCatalog().SelectMany(p => p.Images).ToList();

        Assert.All(images, i => Assert.False(string.IsNullOrWhiteSpace(i.Source)));
        Assert.All(images, i => Assert.False(string.IsNullOrWhiteSpace(i.License)));
        // Photographs published under a Creative Commons licence must name the author and link the licence.
        foreach (var image in images.Where(i => i.License.StartsWith("CC", StringComparison.OrdinalIgnoreCase)))
        {
            Assert.False(string.IsNullOrWhiteSpace(image.Author), $"{image.FileName} has no author");
            Assert.False(string.IsNullOrWhiteSpace(image.LicenseUrl), $"{image.FileName} has no licence link");
        }
    }

    [Fact]
    public void Validation_reports_broken_catalogues()
    {
        var plants = PlantCatalogSeed.LoadCatalog().ToList();
        var first = plants[0];
        plants[0] = first with { Images = first.Images.Select(i => i with { IsPrimary = false }).ToList() };
        plants[1] = plants[1] with { Name = plants[2].Name };
        plants.Add(plants[3]);

        var problems = string.Join("\n", PlantCatalogSeed.Validate(plants));

        Assert.Contains("Expected 39 plants", problems);
        Assert.Contains("needs exactly one primary photograph", problems);
        Assert.Contains("Duplicate answer name", problems);
        Assert.Contains("Duplicate catalogue id", problems);
    }

    // --- import --------------------------------------------------------------

    [Fact]
    public async Task Import_stores_39_plants_and_50_images_and_is_idempotent()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();

        for (var run = 0; run < 3; run++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            await PlantCatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var db = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Assert.Equal(39, await db.Plants.CountAsync());
        Assert.Equal(50, await db.PlantImages.CountAsync());
        Assert.Equal(39, await db.PlantImages.CountAsync(i => i.IsPrimary));
        Assert.Equal(11, await db.PlantImages.CountAsync(i => !i.IsPrimary));
        // Each plant keeps exactly one primary photograph, and the extra ones hang off an existing plant.
        var perPlant = await db.Plants.Select(p => p.Images.Count).ToListAsync();
        Assert.Equal(28, perPlant.Count(c => c == 1));
        Assert.Equal(11, perPlant.Count(c => c == 2));
        Assert.All(perPlant, c => Assert.InRange(c, 1, 2));
    }

    [Fact]
    public async Task Import_never_overwrites_an_administrators_changes()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await PlantCatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        int approvedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // An agronomist confirms one of the conditional names and edits a note.
            var plant = await db.Plants.FirstAsync(p => p.QuizStatus == PlantQuizStatus.Conditional);
            plant.QuizStatus = PlantQuizStatus.Approved;
            plant.BotanicalNote = "Aqronom təsdiqlədi";
            approvedId = plant.Id;
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await PlantCatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var check = verify.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reseeded = await check.Plants.SingleAsync(p => p.Id == approvedId);
        Assert.Equal(PlantQuizStatus.Approved, reseeded.QuizStatus);
        Assert.Equal("Aqronom təsdiqlədi", reseeded.BotanicalNote);
        Assert.Equal(39, await check.Plants.CountAsync());
        Assert.Equal(50, await check.PlantImages.CountAsync());
    }

    [Fact]
    public async Task Conditional_plants_are_imported_but_held_back_from_the_live_quiz()
    {
        await using var factory = new LeaderboardApiFactory();
        await factory.CreateDatabaseAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await PlantCatalogSeed.SeedAsync(db);

        var conditional = await db.Plants.Where(p => p.QuizStatus == PlantQuizStatus.Conditional).Select(p => p.Name).ToListAsync();

        Assert.Equal(9, conditional.Count);
        Assert.Equal(30, await db.Plants.CountAsync(p => p.QuizStatus == PlantQuizStatus.Approved));
        // The two names the review flagged for an agronomist are exactly the ones held back.
        Assert.Contains("Microphyllus", conditional);
        Assert.Contains("Passifloraceae", conditional);
    }

    // --- building a round ----------------------------------------------------

    private static IReadOnlyList<PlantCandidate> Candidates(int plants, int photosEach = 1)
    {
        var id = 0;
        return Enumerable.Range(1, plants)
            .Select(p => new PlantCandidate(p, $"Bitki {p}",
                Enumerable.Range(0, photosEach).Select(_ => { id++; return new PlantPhoto(id, $"/images/plants/plant-{id:000}.webp"); }).ToList()))
            .ToList();
    }

    [Fact]
    public void A_round_asks_ten_different_plants_with_four_different_options()
    {
        var plants = Candidates(30, photosEach: 2);

        for (var seed = 0; seed < 100; seed++)
        {
            var round = PlantQuestionPlanner.Plan(plants, new Random(seed));

            Assert.Equal(QuizRules.QuestionsPerQuiz, round.Count);
            // No plant is asked twice in one session.
            Assert.Equal(round.Count, round.Select(q => q.PlantId).Distinct().Count());

            foreach (var question in round)
            {
                var subject = plants.Single(p => p.PlantId == question.PlantId);
                // Four options, all different plants, and the right one among them.
                Assert.Equal(4, question.Options.Count);
                Assert.Equal(4, question.Options.Distinct(StringComparer.Ordinal).Count());
                Assert.Contains(subject.Name, question.Options);
                Assert.Equal(subject.Name, question.Options["ABCD".IndexOf(question.CorrectDisplayOption)]);
                // The picture belongs to the plant being asked about.
                Assert.Contains(question.ImageId, subject.Photos.Select(p => p.ImageId));
                Assert.Contains(question.ImageUrl, subject.Photos.Select(p => p.ImageUrl));
            }
        }
    }

    [Fact]
    public void A_round_keeps_the_usual_difficulty_ladder_and_scoring()
    {
        var plants = Candidates(30, photosEach: 2);

        for (var seed = 0; seed < 50; seed++)
        {
            var round = PlantQuestionPlanner.Plan(plants, new Random(seed));

            Assert.Equal(QuizRules.EasyPerQuiz, round.Count(q => q.Difficulty == Difficulty.Easy));
            Assert.Equal(QuizRules.MediumPerQuiz, round.Count(q => q.Difficulty == Difficulty.Medium));
            Assert.Equal(QuizRules.HardPerQuiz, round.Count(q => q.Difficulty == Difficulty.Hard));
            Assert.Equal(QuizRules.MaxPoints, round.Sum(q => QuizRules.PointsFor(q.Difficulty)));
        }
    }

    [Fact]
    public void Both_photographs_of_a_plant_are_used_over_many_rounds()
    {
        // A plant with two photographs must be able to appear with either one.
        var plants = Candidates(12, photosEach: 2);
        var seenPerPlant = new Dictionary<int, HashSet<int>>();

        for (var seed = 0; seed < 200; seed++)
        {
            foreach (var q in PlantQuestionPlanner.Plan(plants, new Random(seed)))
            {
                if (!seenPerPlant.TryGetValue(q.PlantId, out var set)) seenPerPlant[q.PlantId] = set = new HashSet<int>();
                set.Add(q.ImageId);
            }
        }

        Assert.All(seenPerPlant.Values, set => Assert.Equal(2, set.Count));
    }

    [Fact]
    public void A_round_needs_enough_approved_plants()
    {
        Assert.False(PlantQuestionPlanner.CanFillRound(9));
        Assert.True(PlantQuestionPlanner.CanFillRound(10));
        Assert.Throws<InvalidOperationException>(() => PlantQuestionPlanner.Plan(Candidates(9), new Random(1)));
    }

    // --- credits endpoint ----------------------------------------------------

    [Fact]
    public async Task Credits_endpoint_publishes_the_attribution_for_all_fifty_photographs()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await PlantCatalogSeed.SeedAsync(scope.ServiceProvider.GetRequiredService<ApplicationDbContext>());
        }

        using var response = await client.GetAsync("/api/plants/credits");
        var raw = await response.Content.ReadAsStringAsync();
        var credits = JsonDocument.Parse(raw).RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(39, credits.GetProperty("plantCount").GetInt32());
        Assert.Equal(50, credits.GetProperty("imageCount").GetInt32());
        Assert.Equal(50, credits.GetProperty("images").GetArrayLength());

        var first = credits.GetProperty("images")[0];
        Assert.Equal(
            new[] { "plantName", "scientificName", "fileName", "imageUrl", "isPrimary", "source", "sourceUrl", "author", "license", "licenseUrl", "specimenSpecies" },
            first.EnumerateObject().Select(p => p.Name));
        Assert.All(credits.GetProperty("images").EnumerateArray(), i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i.GetProperty("source").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(i.GetProperty("license").GetString()));
        });
        // The credits list is public content only: no participant data rides along.
        foreach (var forbidden in new[] { "phone", "participant", "attempt" })
        {
            Assert.DoesNotContain(forbidden, raw, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "public", "images", "plants")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root with frontend/public/images/plants was not found.");
    }
}
