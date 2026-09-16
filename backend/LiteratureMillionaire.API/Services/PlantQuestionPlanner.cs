using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Services;

/// <summary>One photograph a plant question may show.</summary>
public readonly record struct PlantPhoto(int ImageId, string ImageUrl);

/// <summary>A plant that may be used in a live quiz, with the photographs it owns.</summary>
public sealed record PlantCandidate(int PlantId, string Name, IReadOnlyList<PlantPhoto> Photos);

/// <summary>
/// One planned question: which plant is shown, in which photograph, and the four answer options.
/// <c>Options</c> holds the four answer names in the order the player sees them; all four are
/// different plants, and <c>CorrectDisplayOption</c> is the letter of the right one.
/// </summary>
public sealed record PlantQuestionPlan(
    int PlantId,
    int ImageId,
    string ImageUrl,
    IReadOnlyList<string> Options,
    char CorrectDisplayOption,
    Difficulty Difficulty);

/// <summary>
/// Pure, database-free planner for a plant-recognition round.
///
/// Guarantees, per session:
///   - QuizRules.QuestionsPerQuiz questions, each about a different plant (no plant is asked twice);
///   - one photograph per question, drawn at random from that plant's own photographs, so a plant with
///     several photographs can appear with any of them;
///   - four options that are four different plants, one of them the correct one, in random display order;
///   - the usual difficulty ladder (QuizRules.EasyPerQuiz / MediumPerQuiz / HardPerQuiz), so the round
///     scores exactly like every other quiz. Recognition difficulty is not a property of a plant, so the
///     ladder is assigned per session and shuffled over the ten questions.
///
/// Only plants the caller has already filtered to <see cref="PlantQuizStatus.Approved"/> should be passed in.
/// </summary>
public static class PlantQuestionPlanner
{
    /// <summary>Answer options shown per question.</summary>
    public const int OptionsPerQuestion = 4;

    /// <summary>Fewest approved plants a campaign needs: ten distinct questions, four distinct options.</summary>
    public static readonly int MinimumPlants = Math.Max(QuizRules.QuestionsPerQuiz, OptionsPerQuestion);

    private static readonly char[] Letters = { 'A', 'B', 'C', 'D' };

    /// <summary>True when the catalogue can fill a whole round.</summary>
    public static bool CanFillRound(int approvedPlantCount) => approvedPlantCount >= MinimumPlants;

    public static IReadOnlyList<PlantQuestionPlan> Plan(IReadOnlyList<PlantCandidate> plants, Random rng)
    {
        ArgumentNullException.ThrowIfNull(plants);
        ArgumentNullException.ThrowIfNull(rng);

        if (!CanFillRound(plants.Count))
        {
            throw new InvalidOperationException($"A plant round needs at least {MinimumPlants} approved plants, got {plants.Count}.");
        }

        var withoutPhotos = plants.Where(p => p.Photos.Count == 0).Select(p => p.Name).ToArray();
        if (withoutPhotos.Length > 0)
        {
            throw new InvalidOperationException($"These plants have no photograph: {string.Join(", ", withoutPhotos)}.");
        }

        // Ten different plants: the same plant can never be asked twice in one session.
        var asked = Shuffled(plants, rng).Take(QuizRules.QuestionsPerQuiz).ToList();
        var ladder = Shuffled(DifficultyLadder(), rng);

        var planned = new List<PlantQuestionPlan>(asked.Count);
        for (var i = 0; i < asked.Count; i++)
        {
            var subject = asked[i];
            var photo = subject.Photos[rng.Next(subject.Photos.Count)];

            // Three other plants: options are always four different plants, so no option repeats an answer.
            var distractors = Shuffled(plants.Where(p => p.PlantId != subject.PlantId), rng)
                .Take(OptionsPerQuestion - 1);
            var options = Shuffled(distractors.Append(subject), rng);

            planned.Add(new PlantQuestionPlan(
                subject.PlantId,
                photo.ImageId,
                photo.ImageUrl,
                options.Select(o => o.Name).ToArray(),
                Letters[options.FindIndex(o => o.PlantId == subject.PlantId)],
                ladder[i]));
        }

        return planned;
    }

    /// <summary>The fixed difficulty quotas as a flat list of QuizRules.QuestionsPerQuiz tiers.</summary>
    private static List<Difficulty> DifficultyLadder() =>
        new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard }
            .SelectMany(d => Enumerable.Repeat(d, QuizRules.QuotaFor(d)))
            .ToList();

    private static List<T> Shuffled<T>(IEnumerable<T> source, Random rng)
    {
        var list = source.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
        return list;
    }
}
