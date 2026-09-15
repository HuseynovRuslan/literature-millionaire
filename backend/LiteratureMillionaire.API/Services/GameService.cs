using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Campaign quiz engine: 10 random questions from the campaign's quiz mode (and book, when it has one),
/// options shuffled per session, QuizRules.SecondsPerQuestion seconds per question enforced server-side,
/// answered in order, judged against the campaign's PassingScore at the end.
/// </summary>
public class GameService : IGameService
{
    /// <summary>Idle time after which a session is dropped.</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);

    private static readonly TimeSpan QuestionTime = TimeSpan.FromSeconds(QuizRules.SecondsPerQuestion);
    private const string Letters = "ABCD";

    // Unique index names from the migration. A 23505 unique_violation is only ever treated as a
    // resolvable race when it names exactly one of these - anything else is a genuine DB failure.
    private const string ParticipantPhoneUniqueIndex = "IX_Participants_NormalizedPhoneNumber";
    private const string AttemptUniqueIndex = "IX_QuizAttempts_ParticipantId_CampaignId_AttemptNumber";

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ICampaignService _campaigns;
    private readonly ILeaderboardService _leaderboard;
    private readonly ILogger<GameService> _logger;

    public GameService(
        ApplicationDbContext db,
        IMemoryCache cache,
        ICampaignService campaigns,
        ILeaderboardService leaderboard,
        ILogger<GameService> logger)
    {
        _db = db;
        _cache = cache;
        _campaigns = campaigns;
        _leaderboard = leaderboard;
        _logger = logger;
    }

    public async Task<StartGameResponseDto> StartAsync(StartGameRequestDto request, CancellationToken ct = default)
    {
        // The named campaign when it is playable today, otherwise the default mode's campaign (older clients).
        // Only the campaign id comes from the client; rules, image target and book are read from the campaign.
        // Campaign and question checks come first so a limit-free failure never consumes an attempt.
        var campaign = await _campaigns.GetPlayableAsync(request.CampaignId, ct);
        var questions = await SelectQuestionsAsync(campaign, ct);

        var participant = await FindOrCreateParticipantAsync(request, ct);
        var attempt = await CreateAttemptAsync(participant, campaign, ct);

        var session = new GameSession
        {
            CampaignId = campaign.CampaignId,
            ParticipantId = participant.Id,
            AttemptId = attempt.Id,
            BookId = campaign.BookId,
            QuizMode = new QuizModeRefDto(campaign.QuizModeId, campaign.QuizModeSlug, campaign.QuizModeTitle),
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
            Question: first,
            AttemptNumber: attempt.AttemptNumber,
            RemainingAttempts: QuizRules.MaxAttemptsPerCampaign - attempt.AttemptNumber,
            CampaignId: session.CampaignId,
            QuizMode: session.QuizMode);
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

        // Compute first, commit to the session only after any persistence succeeded, so a failed
        // database write leaves the session replayable instead of reporting a result nobody stored.
        var correct = session.CorrectAnswers + (isCorrect ? 1 : 0);
        var points = session.PointsEarned + (isCorrect ? session.Current.Points : 0);
        var isLast = session.CurrentIndex + 1 >= session.TotalQuestions;

        if (isLast)
        {
            var passed = correct >= session.PassingScore;
            await StoreResultAsync(session, correct, points, passed, ct);
        }

        session.CorrectAnswers = correct;
        session.PointsEarned = points;
        session.CurrentIndex++;

        if (isLast)
        {
            session.IsGameOver = true;
            Store(session);

            int? leaderboardPosition = null;
            try
            {
                // The stored attempt is now visible to this query. Ranking always selects this
                // participant's best completed attempt, which may be an earlier attempt.
                leaderboardPosition = await _leaderboard.GetPositionAsync(session.CampaignId, session.ParticipantId, ct);
            }
            catch (Exception ex)
            {
                // Auxiliary ranking must never turn a successfully persisted quiz result into a
                // failure. Do not pass the exception object/message: either can carry provider data.
                _logger.LogWarning(
                    "Leaderboard position lookup failed during {Operation} (campaign {CampaignId}, participant {ParticipantId}, attempt {AttemptId}, {ExceptionType}).",
                    "leaderboard-position", session.CampaignId, session.ParticipantId, session.AttemptId, ex.GetType().Name);
            }

            return new AnswerResultDto(
                QuestionNumber: closedNumber,
                TimedOut: timedOut,
                IsGameOver: true,
                NextQuestionNumber: null,
                NextQuestion: null,
                NextQuestionExpiresAtUtc: null,
                Result: new QuizResultDto(
                    CampaignId: session.CampaignId,
                    LeaderboardPosition: leaderboardPosition,
                    CorrectAnswers: session.CorrectAnswers,
                    TotalQuestions: session.TotalQuestions,
                    PassingScore: session.PassingScore,
                    Passed: session.Passed,
                    RewardTitle: session.Passed ? session.RewardTitle : null,
                    PointsEarned: session.PointsEarned,
                    MaxPoints: session.MaxPoints,
                    QuizMode: session.QuizMode));
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
    /// Loads the pool of the campaign's quiz mode, narrowed to the campaign's book when it has one (id, correct
    /// letter, difficulty, image flag only), and lets <see cref="QuestionMixPlanner"/> build the 3/4/3 mix with the
    /// campaign's image target. Each selected question then gets its own option permutation. Legacy questions
    /// without a quiz mode are never candidates.
    /// </summary>
    private async Task<IReadOnlyList<SessionQuestion>> SelectQuestionsAsync(ActiveCampaign campaign, CancellationToken ct)
    {
        var quizModeId = campaign.QuizModeId;
        var bookId = campaign.BookId;

        var pool = await _db.Questions
            .AsNoTracking()
            .Where(q => q.QuizModeId == quizModeId && (bookId == null || q.BookId == bookId))
            .Select(q => new PoolQuestion(q.Id, q.CorrectOption, q.Difficulty, q.ImageUrl != null))
            .ToListAsync(ct);

        var difficulties = new[] { Difficulty.Easy, Difficulty.Medium, Difficulty.Hard };
        var available = difficulties.ToDictionary(d => d, d => pool.Count(q => q.Difficulty == d));
        var missing = QuestionMixPlanner.Shortfalls(pool);

        if (missing.Count > 0)
        {
            _logger.LogError(
                "Campaign {CampaignId} (quiz mode {QuizModeSlug}, book {BookId}) cannot fill the difficulty mix. Available Easy/Medium/Hard = {Easy}/{Medium}/{Hard}, required {ReqEasy}/{ReqMedium}/{ReqHard}.",
                campaign.CampaignId, campaign.QuizModeSlug, bookId, available[Difficulty.Easy], available[Difficulty.Medium], available[Difficulty.Hard],
                QuizRules.EasyPerQuiz, QuizRules.MediumPerQuiz, QuizRules.HardPerQuiz);

            throw GameException.Conflict(
                "Not enough questions per difficulty for this campaign",
                $"A quiz needs {QuizRules.EasyPerQuiz} Easy, {QuizRules.MediumPerQuiz} Medium and {QuizRules.HardPerQuiz} Hard questions. Missing: {string.Join(", ", missing.Select(d => $"{d} (have {available[d]}, need {QuizRules.QuotaFor(d)})"))}.",
                new Dictionary<string, object?>
                {
                    ["code"] = "INSUFFICIENT_DIFFICULTY_QUESTIONS",
                    ["campaignId"] = campaign.CampaignId,
                    ["quizMode"] = campaign.QuizModeSlug,
                    ["bookId"] = bookId,
                    ["required"] = difficulties.ToDictionary(d => d.ToString().ToLowerInvariant(), d => (object?)QuizRules.QuotaFor(d)),
                    ["available"] = difficulties.ToDictionary(d => d.ToString().ToLowerInvariant(), d => (object?)available[d]),
                    ["missing"] = missing.Select(d => d.ToString().ToLowerInvariant()).ToArray()
                });
        }

        return QuestionMixPlanner.Plan(pool, campaign.ImageQuestionsPerQuiz, Random.Shared)
            .Select(q =>
            {
                // Fisher-Yates over the four original option indices (0 = A .. 3 = D).
                var optionOrder = new[] { 0, 1, 2, 3 };
                Random.Shared.Shuffle(optionOrder);

                var originalIndex = Letters.IndexOf(q.CorrectOption);
                var displayIndex = Array.IndexOf(optionOrder, originalIndex);

                return new SessionQuestion
                {
                    QuestionId = q.Id,
                    OptionOrder = optionOrder,
                    CorrectDisplayOption = Letters[displayIndex],
                    Difficulty = q.Difficulty,
                    Points = QuizRules.PointsFor(q.Difficulty)
                };
            })
            .ToArray();
    }

    // --- participants and attempts ------------------------------------------

    /// <summary>
    /// Finds the participant by normalised phone or creates one. A concurrent creation of the
    /// same number is resolved by re-reading after the unique index rejects the duplicate. The
    /// number itself is never logged.
    /// </summary>
    /// <remarks>
    /// Catches <see cref="Exception"/>, not just <see cref="DbUpdateException"/>: Npgsql can
    /// surface a save failure as <see cref="InvalidOperationException"/> wrapping the
    /// <see cref="DbUpdateException"/> (e.g. when the failure happens while reading back a
    /// server-generated value), so narrowing the catch to <see cref="DbUpdateException"/> alone
    /// lets that shape slip past both the race check and the sanitized-500 fallback and reach
    /// the framework's own unhandled-exception response, which is not sanitized. Classification
    /// still walks the full exception chain for the innermost <see cref="Npgsql.PostgresException"/>,
    /// so this is exactly as precise as catching <see cref="DbUpdateException"/> - it just also
    /// catches the same failure when Npgsql/EF Core wraps it one level further out.
    /// </remarks>
    private async Task<Participant> FindOrCreateParticipantAsync(StartGameRequestDto request, CancellationToken ct)
    {
        if (!PhoneNumber.TryNormalize(request.PhoneNumber, out var phone))
        {
            throw new GameException(StatusCodes.Status400BadRequest, "Invalid phone number", "PhoneNumber must be an Azerbaijani mobile number.",
                new Dictionary<string, object?> { ["code"] = "INVALID_PHONE_NUMBER" });
        }

        var existing = await _db.Participants.FirstOrDefaultAsync(p => p.NormalizedPhoneNumber == phone, ct);
        if (existing is not null)
        {
            return existing;
        }

        var participant = new Participant { FullName = request.FullName.Trim(), NormalizedPhoneNumber = phone, CreatedAtUtc = DateTime.UtcNow };
        _db.Participants.Add(participant);
        try
        {
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Participant {ParticipantId} created.", participant.Id);
            return participant;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _db.Entry(participant).State = EntityState.Detached;
            if (!PostgresErrors.IsUniqueViolationOn(ex, ParticipantPhoneUniqueIndex))
            {
                throw DatabaseFailure("participant-create", ex, participantId: null, campaignId: null);
            }
        }

        // The unique phone index rejected the insert, so a racing request created the row.
        return await _db.Participants.FirstOrDefaultAsync(p => p.NormalizedPhoneNumber == phone, ct)
            ?? throw DatabaseFailure("participant-reread", exception: null, participantId: null, campaignId: null);
    }

    /// <summary>
    /// Consumes one attempt. The count is re-read and the insert retried when the unique
    /// (participant, campaign, attemptNumber) index rejects a racing insert, so parallel
    /// starts can never produce a fourth attempt. Any other database failure is a 500.
    /// </summary>
    private async Task<QuizAttempt> CreateAttemptAsync(Participant participant, ActiveCampaign campaign, CancellationToken ct)
    {
        for (var retry = 0; retry < 3; retry++)
        {
            var used = await CountAttemptsAsync(participant, campaign, ct);
            if (used >= QuizRules.MaxAttemptsPerCampaign)
            {
                throw AttemptLimitReached(used);
            }

            var attempt = new QuizAttempt
            {
                ParticipantId = participant.Id,
                CampaignId = campaign.CampaignId,
                AttemptNumber = used + 1,
                StartedAtUtc = DateTime.UtcNow,
                TotalQuestions = QuizRules.QuestionsPerQuiz,
                PassingScore = campaign.PassingScore,
                MaxPoints = QuizRules.MaxPoints
            };
            _db.QuizAttempts.Add(attempt);
            try
            {
                await _db.SaveChangesAsync(ct);
                _logger.LogInformation("Attempt {AttemptNumber} started for participant {ParticipantId} in campaign {CampaignId}.", attempt.AttemptNumber, participant.Id, campaign.CampaignId);
                return attempt;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _db.Entry(attempt).State = EntityState.Detached;
                if (!PostgresErrors.IsUniqueViolationOn(ex, AttemptUniqueIndex))
                {
                    throw DatabaseFailure("attempt-create", ex, participant.Id, campaign.CampaignId);
                }
                // Number taken by a racing start: recount and retry.
            }
        }

        // Every retry lost the race. If the racers used the limit up, that is the real answer;
        // only an unresolved race below the limit is reported as a retryable conflict.
        var finalCount = await CountAttemptsAsync(participant, campaign, ct);
        if (finalCount >= QuizRules.MaxAttemptsPerCampaign)
        {
            throw AttemptLimitReached(finalCount);
        }

        _logger.LogWarning("Attempt for participant {ParticipantId} in campaign {CampaignId} lost the insert race 3 times at {AttemptsUsed} attempts used.", participant.Id, campaign.CampaignId, finalCount);
        throw GameException.Conflict("Attempt could not be recorded", "Could not record the attempt after several tries. Please try again.",
            new Dictionary<string, object?> { ["code"] = "ATTEMPT_CONFLICT" });
    }

    private Task<int> CountAttemptsAsync(Participant participant, ActiveCampaign campaign, CancellationToken ct) =>
        _db.QuizAttempts.CountAsync(a => a.ParticipantId == participant.Id && a.CampaignId == campaign.CampaignId, ct);

    private static GameException AttemptLimitReached(int used) =>
        GameException.Conflict("Attempt limit reached",
            $"This phone number has used all {QuizRules.MaxAttemptsPerCampaign} attempts for the current campaign.",
            new Dictionary<string, object?> { ["code"] = "ATTEMPT_LIMIT_REACHED", ["maxAttempts"] = QuizRules.MaxAttemptsPerCampaign, ["attemptsUsed"] = used });

    /// <summary>
    /// Logs an unexpected database failure in sanitized form - operation, ids, SQLSTATE, constraint
    /// name and exception types only. The exception object is deliberately not passed to the
    /// logger: PostgreSQL's message/detail/where text can quote row values such as the phone
    /// number. The constraint name is safe to log - it is a schema identifier from our own
    /// migration, never derived from row data.
    /// </summary>
    private GameException DatabaseFailure(string operation, Exception? exception, int? participantId, int? campaignId)
    {
        var sqlState = exception is null ? null : PostgresErrors.GetSqlState(exception);
        var constraintName = exception is null ? null : PostgresErrors.GetConstraintName(exception);
        _logger.LogError(
            "Database failure during {Operation} (participant {ParticipantId}, campaign {CampaignId}, sql state {SqlState}, constraint {ConstraintName}, {ExceptionType} / {InnerExceptionType}).",
            operation, participantId, campaignId, sqlState, constraintName, exception?.GetType().Name, exception?.GetBaseException().GetType().Name);
        return GameException.DatabaseError();
    }

    /// <summary>Writes the server-computed result exactly once (only where CompletedAtUtc is still null). Throws if nothing was stored, so no unstored result is ever reported.</summary>
    private async Task StoreResultAsync(GameSession session, int correct, int points, bool passed, CancellationToken ct)
    {
        var rows = await _db.QuizAttempts
            .Where(a => a.Id == session.AttemptId && a.CompletedAtUtc == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(a => a.CompletedAtUtc, DateTime.UtcNow)
                .SetProperty(a => a.CorrectAnswers, correct)
                .SetProperty(a => a.PointsEarned, points)
                .SetProperty(a => a.Passed, passed), ct);

        if (rows != 1)
        {
            _logger.LogError("Result for attempt {AttemptId} could not be stored (rows affected: {Rows}).", session.AttemptId, rows);
            throw new InvalidOperationException($"Result for attempt {session.AttemptId} could not be stored.");
        }
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
