namespace LiteratureMillionaire.API.Services;

/// <summary>Fixed rules of the "Ayın kitabı" campaign quiz, shared by campaign and game code.</summary>
public static class QuizRules
{
    /// <summary>Questions per quiz session. PassingScore on a campaign is measured against this.</summary>
    public const int QuestionsPerQuiz = 10;

    /// <summary>Server-enforced time a player has for each question. The client countdown only mirrors it.</summary>
    public const int SecondsPerQuestion = 15;

    /// <summary>
    /// Preferred number of illustrated questions per quiz. Applied only when the book has at
    /// least this many illustrated and enough text-only questions; otherwise selection falls
    /// back to any QuestionsPerQuiz questions so smaller campaigns are never blocked.
    /// </summary>
    public const int ImageQuestionsPerQuiz = 2;
}
