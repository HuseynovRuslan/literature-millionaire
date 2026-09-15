using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Services;

/// <summary>Minimal view of a candidate question used for session planning.</summary>
public readonly record struct PoolQuestion(int Id, char CorrectOption, Difficulty Difficulty, bool HasImage);

/// <summary>
/// Pure, database-free planner for a session's question mix. Guarantees the fixed
/// difficulty quotas (QuizRules.EasyPerQuiz / MediumPerQuiz / HardPerQuiz) and aims for
/// exactly the given image target (the campaign's ImageQuestionsPerQuiz) illustrated questions.
///
/// Image count rule, per difficulty d with quota q, i illustrated and t text-only questions:
///   forced  = max(0, q - t)   images that must be used because text alone cannot fill the quota
///   ceiling = min(i, q)       images that could be used at most
/// The session image count is the image target clamped to [sum(forced), sum(ceiling)]:
///   - exactly the target whenever the pool allows it (the normal case);
///   - fewer when the pool has fewer usable illustrations (never blocks a campaign);
///   - more only when a difficulty has too few text questions and illustrated ones
///     are needed to reach the quota (quota and 10 questions take priority over the image target).
/// Extra images beyond the forced minimum are spread over difficulties at random.
/// </summary>
public static class QuestionMixPlanner
{
    private static readonly Difficulty[] Difficulties = { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };

    /// <summary>Difficulties whose quota cannot be met by the pool at all (images and text together). Empty when a plan is possible.</summary>
    public static IReadOnlyList<Difficulty> Shortfalls(IReadOnlyList<PoolQuestion> pool) =>
        Difficulties.Where(d => pool.Count(q => q.Difficulty == d) < QuizRules.QuotaFor(d)).ToArray();

    /// <summary>
    /// Returns exactly QuizRules.QuestionsPerQuiz distinct questions in random order,
    /// honouring the quotas and the image rule described above.
    /// </summary>
    /// <param name="pool">Candidate questions.</param>
    /// <param name="imageTarget">Preferred number of illustrated questions, 0..QuizRules.QuestionsPerQuiz.</param>
    /// <param name="rng">Randomness source.</param>
    /// <exception cref="ArgumentOutOfRangeException">When <paramref name="imageTarget"/> is outside 0..QuizRules.QuestionsPerQuiz.</exception>
    /// <exception cref="InvalidOperationException">When a difficulty quota cannot be met; callers should check <see cref="Shortfalls"/> first.</exception>
    public static IReadOnlyList<PoolQuestion> Plan(IReadOnlyList<PoolQuestion> pool, int imageTarget, Random rng)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(imageTarget);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(imageTarget, QuizRules.QuestionsPerQuiz);

        var shortfalls = Shortfalls(pool);
        if (shortfalls.Count > 0)
        {
            throw new InvalidOperationException($"Pool cannot fill the difficulty quota for: {string.Join(", ", shortfalls)}.");
        }

        var images = Difficulties.ToDictionary(d => d, d => Shuffled(pool.Where(q => q.Difficulty == d && q.HasImage), rng));
        var texts = Difficulties.ToDictionary(d => d, d => Shuffled(pool.Where(q => q.Difficulty == d && !q.HasImage), rng));

        var forced = Difficulties.ToDictionary(d => d, d => Math.Max(0, QuizRules.QuotaFor(d) - texts[d].Count));
        var ceiling = Difficulties.ToDictionary(d => d, d => Math.Min(images[d].Count, QuizRules.QuotaFor(d)));

        var target = Math.Clamp(imageTarget, forced.Values.Sum(), ceiling.Values.Sum());
        var imageCount = new Dictionary<Difficulty, int>(forced);

        // Spread the optional images over difficulties that still have headroom, at random.
        var headroom = Difficulties.SelectMany(d => Enumerable.Repeat(d, ceiling[d] - forced[d])).ToList();
        foreach (var d in Shuffled(headroom, rng).Take(target - forced.Values.Sum()))
        {
            imageCount[d]++;
        }

        var chosen = new List<PoolQuestion>(QuizRules.QuestionsPerQuiz);
        foreach (var d in Difficulties)
        {
            chosen.AddRange(images[d].Take(imageCount[d]));
            chosen.AddRange(texts[d].Take(QuizRules.QuotaFor(d) - imageCount[d]));
        }

        return Shuffled(chosen, rng); // neither difficulty nor illustrated questions sit in fixed slots
    }

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
