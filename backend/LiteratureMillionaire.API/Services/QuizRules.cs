using LiteratureMillionaire.API.Entities;

namespace LiteratureMillionaire.API.Services;

/// <summary>Fixed rules of the "Ayın kitabı" campaign quiz, shared by campaign and game code.</summary>
public static class QuizRules
{
    /// <summary>Questions per quiz session. PassingScore on a campaign is measured against this.</summary>
    public const int QuestionsPerQuiz = 10;

    /// <summary>Server-enforced time a player has for each question. The client countdown only mirrors it.</summary>
    public const int SecondsPerQuestion = 10;

    /// <summary>
    /// Preferred number of illustrated questions per quiz. Applied only when the book has at
    /// least this many illustrated and enough text-only questions; otherwise selection falls
    /// back to any QuestionsPerQuiz questions so smaller campaigns are never blocked.
    /// </summary>
    public const int ImageQuestionsPerQuiz = 2;

    /// <summary>Fixed difficulty mix per quiz so every player faces the same maximum score.</summary>
    public const int EasyPerQuiz = 3;
    public const int MediumPerQuiz = 4;
    public const int HardPerQuiz = 3;

    /// <summary>Points for a correct answer by difficulty. Wrong, late and timed-out answers score 0.</summary>
    public static int PointsFor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => 1,
        Difficulty.Medium => 2,
        Difficulty.Hard => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Unknown difficulty.")
    };

    public static int QuotaFor(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => EasyPerQuiz,
        Difficulty.Medium => MediumPerQuiz,
        Difficulty.Hard => HardPerQuiz,
        _ => 0
    };

    /// <summary>3x1 + 4x2 + 3x3 = 20 with the current mix.</summary>
    public static int MaxPoints =>
        EasyPerQuiz * PointsFor(Difficulty.Easy) + MediumPerQuiz * PointsFor(Difficulty.Medium) + HardPerQuiz * PointsFor(Difficulty.Hard);
}
