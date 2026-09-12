namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Single source of truth for the 15-step prize ladder. Values are game points, not currency.
/// </summary>
public static class PrizeLadder
{
    public const int TotalQuestions = 15;
    public const int QuestionsPerDifficulty = 5;

    private static readonly int[] Prizes =
    {
        100, 200, 300, 500, 1_000,
        2_000, 4_000, 8_000, 16_000, 32_000,
        64_000, 125_000, 250_000, 500_000, 1_000_000
    };

    public static IReadOnlyList<int> All => Prizes;

    public static int TopPrize => Prizes[^1];

    /// <summary>Prize awarded for correctly answering the given 1-based question number.</summary>
    public static int PrizeFor(int questionNumber)
    {
        if (questionNumber is < 1 or > TotalQuestions)
        {
            throw new ArgumentOutOfRangeException(nameof(questionNumber), questionNumber, $"Must be between 1 and {TotalQuestions}.");
        }

        return Prizes[questionNumber - 1];
    }
}
