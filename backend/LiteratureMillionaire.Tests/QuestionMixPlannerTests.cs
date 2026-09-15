using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class QuestionMixPlannerTests
{
    private static int _nextId;

    /// <summary>Builds a pool: (difficulty, illustrated count, text-only count) per entry.</summary>
    private static List<PoolQuestion> Pool(params (Difficulty d, int images, int texts)[] spec)
    {
        var pool = new List<PoolQuestion>();
        foreach (var (d, images, texts) in spec)
        {
            for (var i = 0; i < images; i++) pool.Add(new PoolQuestion(++_nextId, 'A', d, HasImage: true));
            for (var i = 0; i < texts; i++) pool.Add(new PoolQuestion(++_nextId, 'B', d, HasImage: false));
        }
        return pool;
    }

    private static void AssertFullMix(IReadOnlyList<PoolQuestion> plan, IReadOnlyList<PoolQuestion> pool)
    {
        Assert.Equal(QuizRules.QuestionsPerQuiz, plan.Count);
        Assert.Equal(plan.Count, plan.Select(q => q.Id).Distinct().Count());
        Assert.All(plan, q => Assert.Contains(q, pool));
        Assert.Equal(QuizRules.EasyPerQuiz, plan.Count(q => q.Difficulty == Difficulty.Easy));
        Assert.Equal(QuizRules.MediumPerQuiz, plan.Count(q => q.Difficulty == Difficulty.Medium));
        Assert.Equal(QuizRules.HardPerQuiz, plan.Count(q => q.Difficulty == Difficulty.Hard));
        Assert.Equal(QuizRules.MaxPoints, plan.Sum(q => QuizRules.PointsFor(q.Difficulty)));
    }

    [Fact]
    public void EasyQuotaNeedsIllustratedQuestions_UsesThemAndStillFillsTenQuestions()
    {
        // The reported edge case: Easy = 2 illustrated + 1 text, Medium = 4 text, Hard = 3 text.
        var pool = Pool((Difficulty.Easy, 2, 1), (Difficulty.Medium, 0, 4), (Difficulty.Hard, 0, 3));

        for (var seed = 0; seed < 50; seed++)
        {
            var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(seed));
            AssertFullMix(plan, pool);
            Assert.Equal(2, plan.Count(q => q.HasImage));
            Assert.All(plan.Where(q => q.HasImage), q => Assert.Equal(Difficulty.Easy, q.Difficulty));
        }
    }

    [Fact]
    public void TwoImagesOnlyPossibleFromOneDifficulty_PicksBothFromIt()
    {
        // Illustrations exist only among Hard questions; text is plentiful everywhere.
        var pool = Pool((Difficulty.Easy, 0, 10), (Difficulty.Medium, 0, 12), (Difficulty.Hard, 2, 6));

        for (var seed = 0; seed < 50; seed++)
        {
            var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(seed));
            AssertFullMix(plan, pool);
            Assert.Equal(2, plan.Count(q => q.HasImage));
            Assert.All(plan.Where(q => q.HasImage), q => Assert.Equal(Difficulty.Hard, q.Difficulty));
        }
    }

    [Fact]
    public void OlulerShapedBank_AlwaysThreeFourThreeWithTwoImagesInVaryingPlaces()
    {
        // Current approved bank: Easy 10 (3 illustrated), Medium 12 (1), Hard 8 (2).
        var pool = Pool((Difficulty.Easy, 3, 7), (Difficulty.Medium, 1, 11), (Difficulty.Hard, 2, 6));
        var imageSlots = new HashSet<int>();
        var imageDifficultyPairs = new HashSet<string>();

        for (var seed = 0; seed < 200; seed++)
        {
            var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(seed));
            AssertFullMix(plan, pool);
            Assert.Equal(2, plan.Count(q => q.HasImage));
            for (var i = 0; i < plan.Count; i++) if (plan[i].HasImage) imageSlots.Add(i);
            imageDifficultyPairs.Add(string.Join("+", plan.Where(q => q.HasImage).Select(q => q.Difficulty).OrderBy(d => d)));
        }

        Assert.True(imageSlots.Count >= 8, $"illustrated questions should move around; slots seen: {imageSlots.Count}");
        Assert.True(imageDifficultyPairs.Count >= 3, $"illustrated questions should come from varying difficulties; pairs: {string.Join(", ", imageDifficultyPairs)}");
    }

    [Fact]
    public void NoIllustrations_StillFullMixWithZeroImages()
    {
        var pool = Pool((Difficulty.Easy, 0, 5), (Difficulty.Medium, 0, 6), (Difficulty.Hard, 0, 4));
        var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(1));
        AssertFullMix(plan, pool);
        Assert.Equal(0, plan.Count(q => q.HasImage));
    }

    [Fact]
    public void OnlyOneIllustration_UsesOne()
    {
        var pool = Pool((Difficulty.Easy, 1, 5), (Difficulty.Medium, 0, 6), (Difficulty.Hard, 0, 4));
        var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(2));
        AssertFullMix(plan, pool);
        Assert.Equal(1, plan.Count(q => q.HasImage));
    }

    [Fact]
    public void TextTooScarce_UsesMoreThanTwoImagesRatherThanBreakingQuota()
    {
        // Easy has no text at all: all three Easy slots must be illustrated (documented fallback).
        var pool = Pool((Difficulty.Easy, 3, 0), (Difficulty.Medium, 0, 4), (Difficulty.Hard, 0, 3));
        var plan = QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(3));
        AssertFullMix(plan, pool);
        Assert.Equal(3, plan.Count(q => q.HasImage));
    }

    [Fact]
    public void QuotaImpossible_ReportsShortfallAndThrows()
    {
        var pool = Pool((Difficulty.Easy, 1, 1), (Difficulty.Medium, 0, 4), (Difficulty.Hard, 0, 3));
        Assert.Equal(new[] { Difficulty.Easy }, QuestionMixPlanner.Shortfalls(pool));
        Assert.Throws<InvalidOperationException>(() => QuestionMixPlanner.Plan(pool, QuizRules.DefaultImageQuestionsPerQuiz, new Random(4)));
    }
}
