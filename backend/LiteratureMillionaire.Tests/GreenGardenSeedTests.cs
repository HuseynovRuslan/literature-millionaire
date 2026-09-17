using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// The generated "Green Garden Kolleksiyası" bank: what the catalogue produced, that every picture is
/// really there, and that a cultivar is never offered against another cultivar of its own genus.
/// </summary>
public class GreenGardenSeedTests
{
    private static readonly IReadOnlyList<GreenGardenSeed.SeedQuestion> Bank = GreenGardenSeed.LoadQuestions();

    [Fact]
    public void Bank_is_valid_and_every_difficulty_can_fill_a_round()
    {
        Assert.Empty(GreenGardenSeed.Validate(Bank));
        Assert.Equal(GreenGardenSeed.ExpectedQuestionCount, Bank.Count);
        Assert.All(Enum.GetValues<Difficulty>(), d => Assert.True(
            Bank.Count(q => q.Difficulty == d) >= 20, $"too few {d} questions"));
    }

    [Fact]
    public void Every_picture_is_a_real_webp_in_frontend_public_and_is_credited_to_its_page()
    {
        var folder = Path.Combine(RepositoryRoot(), "frontend", "public", "question-images");

        Assert.All(Bank, q => Assert.Matches(@"^/question-images/gg-\d{3}\.webp$", q.ImageUrl));
        Assert.All(Bank, q => Assert.True(
            File.Exists(Path.Combine(folder, Path.GetFileName(q.ImageUrl))), $"missing file: {q.ImageUrl}"));
        Assert.Equal(Bank.Count, Directory.GetFiles(folder, "gg-*.webp").Length);
        // The catalogue is somebody's work even when that somebody said yes, so the page stays with it.
        Assert.All(Bank, q => Assert.Matches(@"Green Garden.*səh\. \d+", q.ImageSource));
    }

    [Fact]
    public void No_cultivar_is_offered_against_another_cultivar_of_its_own_genus()
    {
        // Thirteen Acer palmatum cultivars are one photograph thirteen times over to anybody's eye. Keeping
        // them as separate questions is only defensible while they never meet in one question's options.
        foreach (var question in Bank)
        {
            var genus = GreenGardenSeed.SeedQuestion.GenusOf(question.Answer);
            var clashes = question.Options
                .Where(o => o != question.Answer && GreenGardenSeed.SeedQuestion.GenusOf(o) == genus)
                .ToList();

            Assert.True(clashes.Count == 0,
                $"{question.SourceId}: {question.Answer} is offered beside {string.Join(", ", clashes)}.");
        }
    }

    [Fact]
    public void Every_option_leads_with_the_name_the_catalogue_prints()
    {
        // "Abelia grandiflora 'Edward Goucher' (Abeliya)", or the Latin alone where no Azerbaijani name
        // could be sourced. What may never appear is a name that came from nowhere.
        Assert.All(Bank, q => Assert.All(q.Options,
            o => Assert.Matches(@"^[A-Z][a-z]+( [a-z][a-z\-]+)?( '[^']+')?( \(\S.*\))?$", o)));

        // The catalogue prints Latin only, so a good half of the bank has no sourced Azerbaijani name and
        // says so by staying silent. If this ever reaches zero, someone has started inventing names again.
        var withAzerbaijani = Bank.Count(q => q.Answer.EndsWith(')'));
        Assert.InRange(withAzerbaijani, 1, Bank.Count - 1);
    }

    [Fact]
    public void No_alt_text_or_question_gives_its_answer_away()
    {
        Assert.All(Bank, q => Assert.DoesNotContain(q.Answer, q.ImageAltText, StringComparison.CurrentCultureIgnoreCase));
        Assert.All(Bank, q => Assert.DoesNotContain(q.Answer, q.Text, StringComparison.CurrentCultureIgnoreCase));
    }

    [Fact]
    public void The_answer_is_spread_across_all_four_letters()
    {
        var byLetter = Bank.GroupBy(q => q.CorrectOption).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(4, byLetter.Count);
        Assert.All(byLetter.Values, count => Assert.InRange(count, Bank.Count / 8, Bank.Count / 2));
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "public", "question-images")))
            {
                return dir.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root with frontend/public/question-images was not found.");
    }
}
