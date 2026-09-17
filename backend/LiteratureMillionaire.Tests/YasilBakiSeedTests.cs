using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// The generated "Yaşıl Bakı" bank: what the data file contains, that every picture is really there, and
/// that no question can be answered by elimination or lost to a lookalike.
/// </summary>
public class YasilBakiSeedTests
{
    private static readonly IReadOnlyList<YasilBakiSeed.SeedQuestion> Bank = YasilBakiSeed.LoadQuestions();

    [Fact]
    public void Bank_is_valid_and_every_difficulty_can_fill_a_round()
    {
        Assert.Empty(YasilBakiSeed.Validate(Bank));
        Assert.Equal(YasilBakiSeed.ExpectedQuestionCount, Bank.Count);
        // A round draws 3 / 4 / 3, so each difficulty needs enough rows that two players do not meet the
        // same ten questions.
        Assert.All(Enum.GetValues<Difficulty>(), d => Assert.True(
            Bank.Count(q => q.Difficulty == d) >= 20, $"too few {d} questions"));
    }

    [Fact]
    public void Every_photograph_is_a_real_webp_in_frontend_public_and_carries_its_credit()
    {
        var folder = Path.Combine(RepositoryRoot(), "frontend", "public", "question-images");

        Assert.All(Bank, q => Assert.Matches(@"^/question-images/plant-\d{3}-\d\.webp$", q.ImageUrl));
        Assert.All(Bank, q => Assert.True(
            File.Exists(Path.Combine(folder, Path.GetFileName(q.ImageUrl))), $"missing file: {q.ImageUrl}"));
        Assert.Equal(Bank.Count, Directory.GetFiles(folder, "plant-*.webp").Length);
        // Creative Commons terms require the credit to stay with the picture; the public-domain marks do
        // not, but the photographer is named anyway. Anything outside these two families was never taken.
        Assert.All(Bank, q => Assert.False(string.IsNullOrWhiteSpace(q.ImageSource)));
        Assert.All(Bank, q => Assert.True(
            q.ImageLicense.StartsWith("CC", StringComparison.Ordinal)
            || q.ImageLicense.StartsWith("Public Domain", StringComparison.Ordinal),
            $"{q.SourceId}: unexpected licence '{q.ImageLicense}'"));
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
        // Nothing like an even split is required, but a letter that carries half the bank would be a
        // strategy rather than a quiz.
        Assert.All(byLetter.Values, count => Assert.InRange(count, Bank.Count / 8, Bank.Count / 2));
    }

    [Fact]
    public void Every_option_is_named_the_way_a_garden_label_names_a_plant()
    {
        // "Punica granatum (Adi nar)": the scientific name, which is the one that was actually verified
        // and reads the same in every language, and the Azerbaijani name for the player who knows only
        // that one. The review screen already showed both; the options used to show neither.
        Assert.All(Bank, q => Assert.All(q.Options,
            o => Assert.Matches(@"^[A-Z][a-z]+ [a-z][a-z\-]+ \(\S.*\)$", o)));
    }

    [Fact]
    public void No_question_offers_a_plant_that_looks_like_its_own_answer()
    {
        // The whole bank rests on this: a photograph of a cedar is unanswerable when another cedar is on
        // the list, and that is a fault of the options, not of the picture. Every option opens with its
        // genus, so the check needs nothing but the option itself.
        static string Genus(string option) => option.Split(' ')[0];

        foreach (var question in Bank)
        {
            var clashes = question.Options
                .Where(o => o != question.Answer && Genus(o) == Genus(question.Answer))
                .ToList();

            Assert.True(clashes.Count == 0,
                $"{question.SourceId}: {question.Answer} is offered beside {string.Join(", ", clashes)} of the same genus.");
        }
    }

    [Fact]
    public void Each_plant_is_asked_about_from_a_different_side()
    {
        // Several questions per plant is the point - fruit, then flower, then leaf. The same wording twice
        // for one plant would mean two pictures of the same thing.
        foreach (var plant in Bank.GroupBy(q => q.Answer, StringComparer.Ordinal))
        {
            Assert.Equal(plant.Count(), plant.Select(q => q.Text).Distinct(StringComparer.Ordinal).Count());
        }
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
