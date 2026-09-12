using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// "Ayın kitabı" quiz engine: 10 random questions from the active campaign's book,
/// options shuffled per session, 30 seconds per question enforced server-side,
/// answered in order, judged against the campaign's PassingScore at the end.
/// </summary>
public class GameService : IGameService
{
    /// <summary>Idle time after which a session is dropped.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan QuestionTime = TimeSpan.FromSeconds(QuizRules.SecondsPerQuestion);
    private const string Letters = "ABCD";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICampaignService _campaigns;
    private readonly ILogger<GameService> _logger;

    public GameService(ApplicationDbContext db, IMemoryCache cache, ICampaignService campaigns, ILogger<GameService> logger)
    {
        _db = db;
        _cache = cache;
        _campaigns = campaigns;
        _logger = logger;
    }

    public async Task<StartGameResponseDto> StartAsync(CancellationToken ct = default)
    {
        // Same selection rules and same calendar date as GET /api/campaigns/current.
        var campaign = await _campaigns.GetCurrentAsync(ct);
        var questions = await SelectQuestionsAsync(campaign, ct);

        var session = new GameSession
        {
            CampaignId = campaign.CampaignId,
            BookId = campaign.Book.Id,
            PassingScore = campaign.PassingScore,
            RewardTitle = campaign.RewardTitle,
            Questions = questions,
            QuestionDeadlineUtc = DateTime.UtcNow.Add(QuestionTime),
            ExpiresAt = DateTime.UtcNow.Add(SessionLifetime)
        };
        Store(session);

        var first = await LoadQuestionAsync(session.Current, ct);

        return new StartGameResponseDto(
            SessionId: session.SessionId,
            QuestionNumber: session.CurrentQuestionNumber,
            TotalQuestions: session.TotalQuestions,
            PassingScore: session.PassingScore,
            SecondsPerQuestion: QuizRules.SecondsPerQuestion,
            QuestionExpiresAtUtc: session.QuestionDeadlineUtc,
            Question: first);
    }

    public async Task<AnswerResultDto> AnswerAsync(Guid sessionId, SubmitAnswerDto dto, CancellationToken ct = default)
    {
        var session = GetSession(sessionId);

        await session.Gate.WaitAsync(ct);
        try
        {
            EnsureCurrent(session, dto.QuestionId);

            // The server clock decides. A late answer is never evaluated, even if it would be right.
            var late = DateTime.UtcNow > session.QuestionDeadlineUtc;
            var isCorrect = !late && dto.SelectedOption[0] == session.Current.CorrectDisplayOption;

            return await CompleteCurrentAsync(session, isCorrect, timedOut: late, ct);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    public async Task<AnswerResultDto> TimeoutAsync(Guid sessionId, TimeoutRequestDto dto, CancellationToken ct = default)
    {
        var session = GetSession(sessionId);

        await session.Gate.WaitAsync(ct);
        try
        {
            EnsureCurrent(session, dto.QuestionId);

            var remaining = session.QuestionDeadlineUtc - DateTime.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                throw GameException.Conflict("Question time remaining",
                    $"Question #{session.CurrentQuestionNumber} can still be answered for {Math.Ceiling(remaining.TotalSeconds):0} s.",
                    new Dictionary<string, object?>
                    {
                        ["code"] = "QUESTION_TIME_REMAINING",
                        ["questionExpiresAtUtc"] = session.QuestionDeadlineUtc,
                        ["remainingSeconds"] = Math.Ceiling(remaining.TotalSeconds)
                    });
            }

            return await CompleteCurrentAsync(session, isCorrect: false, timedOut: true, ct);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    // --- shared progression -------------------------------------------------

    /// <summary>
    /// The single place where a question is closed and the quiz moves on. Used by
    /// both answers and timeouts so the game can never advance twice for one question.
    /// Caller must hold the session gate.
    /// </summary>
    private async Task<AnswerResultDto> CompleteCurrentAsync(GameSession session, bool isCorrect, bool timedOut, CancellationToken ct)
    {
        var closedNumber = session.CurrentQuestionNumber;

        if (isCorrect)
        {
            session.CorrectAnswers++;
        }
        session.CurrentIndex++;

        if (session.CurrentIndex >= session.TotalQuestions)
        {
            session.IsGameOver = true;
            Store(session);

            return new AnswerResultDto(
                QuestionNumber: closedNumber,
                TimedOut: timedOut,
                IsGameOver: true,
                NextQuestionNumber: null,
                NextQuestion: null,
                NextQuestionExpiresAtUtc: null,
                Result: new QuizResultDto(
                    CorrectAnswers: session.CorrectAnswers,
                    TotalQuestions: session.TotalQuestions,
                    PassingScore: session.PassingScore,
                    Passed: session.Passed,
                    RewardTitle: session.Passed ? session.RewardTitle : null));
        }

        // The next question's clock starts now, on the server, regardless of client latency.
        session.QuestionDeadlineUtc = DateTime.UtcNow.Add(QuestionTime);
        Store(session);
        var next = await LoadQuestionAsync(session.Current, ct);

        return new AnswerResultDto(
            QuestionNumber: closedNumber,
            TimedOut: timedOut,
            IsGameOver: false,
            NextQuestionNumber: session.CurrentQuestionNumber,
            NextQuestion: next,
            NextQuestionExpiresAtUtc: session.QuestionDeadlineUtc,
            Result: null);
    }

    // --- helpers -----------------------------------------------------------

    private GameSession GetSession(Guid sessionId) =>
        _cache.Get<GameSession>(CacheKey(sessionId))
        ?? throw GameException.NotFound($"Session '{sessionId}' does not exist or has expired.");

    /// <summary>Rejects calls for a finished quiz or for any question other than the current one (stale, duplicate, out of order).</summary>
    private static void EnsureCurrent(GameSession session, int questionId)
    {
        if (session.IsGameOver)
        {
            throw GameException.Conflict("Game is over",
                "This quiz has already ended. Start a new game.",
                new Dictionary<string, object?> { ["code"] = "GAME_OVER", ["correctAnswers"] = session.CorrectAnswers, ["passed"] = session.Passed });
        }

        if (questionId != session.CurrentQuestionId)
        {
            throw GameException.Conflict("Unexpected question",
                $"Question {questionId} is not the current question. The quiz is at question #{session.CurrentQuestionNumber} (id {session.CurrentQuestionId}).",
                new Dictionary<string, object?> { ["code"] = "UNEXPECTED_QUESTION", ["expectedQuestionId"] = session.CurrentQuestionId, ["questionNumber"] = session.CurrentQuestionNumber });
        }
    }

    /// <summary>
    /// Random QuizRules.QuestionsPerQuiz questions from the campaign book's pool, each
    /// with its own per-session option permutation. Only id + correct letter are read
    /// here; legacy questions without a book are never candidates.
    /// </summary>
    private async Task<IReadOnlyList<SessionQuestion>> SelectQuestionsAsync(CurrentCampaignDto campaign, CancellationToken ct)
    {
        var bookId = campaign.Book.Id;

        var pool = await _db.Questions
            .AsNoTracking()
            .Where(q => q.BookId == bookId)
            .Select(q => new { q.Id, q.CorrectOption })
            .ToArrayAsync(ct);

        if (pool.Length < QuizRules.QuestionsPerQuiz)
        {
            _logger.LogError(
                "Campaign {CampaignId} (book {BookId}) has {Available} questions; {Required} are required to start a quiz.",
                campaign.CampaignId, bookId, pool.Length, QuizRules.QuestionsPerQuiz);

            throw GameException.Conflict(
                "Not enough questions for this campaign",
                $"The campaign's book has {pool.Length} questions; {QuizRules.QuestionsPerQuiz} are required.",
                new Dictionary<string, object?>
                {
                    ["code"] = "INSUFFICIENT_CAMPAIGN_QUESTIONS",
                    ["campaignId"] = campaign.CampaignId,
                    ["bookId"] = bookId,
                    ["required"] = QuizRules.QuestionsPerQuiz,
                    ["available"] = pool.Length
                });
        }

        Random.Shared.Shuffle(pool);

        return pool
            .Take(QuizRules.QuestionsPerQuiz)
            .Select(q =>
            {
                // Fisher-Yates over the four original option indices (0 = A .. 3 = D).
                var order = new[] { 0, 1, 2, 3 };
                Random.Shared.Shuffle(order);

                var originalIndex = Letters.IndexOf(q.CorrectOption);
                var displayIndex = Array.IndexOf(order, originalIndex);

                return new SessionQuestion
                {
                    QuestionId = q.Id,
                    OptionOrder = order,
                    CorrectDisplayOption = Letters[displayIndex]
                };
            })
            .ToArray();
    }

    private async Task<GameQuestionDto> LoadQuestionAsync(SessionQuestion sq, CancellationToken ct)
    {
        var entity = await _db.Questions.AsNoTracking().FirstOrDefaultAsync(q => q.Id == sq.QuestionId, ct)
            ?? throw GameException.Conflict(
                "Question unavailable",
                $"Question {sq.QuestionId} was removed while the game was in progress. Start a new game.");

        return GameQuestionDto.FromEntity(entity, sq.OptionOrder);
    }

    private void Store(GameSession session)
    {
        session.ExpiresAt = DateTime.UtcNow.Add(SessionLifetime);
        _cache.Set(CacheKey(session.SessionId), session, new MemoryCacheEntryOptions
        {
            SlidingExpiration = SessionLifetime
        });
    }

    private static string CacheKey(Guid sessionId) => $"game-session:{sessionId}";
}
