using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class LeaderboardRankingTests
{
    private static readonly DateTime BaseTime = new(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);

    private static LeaderboardCandidate Candidate(
        int attemptId,
        int participantId,
        int points,
        int correct,
        int durationSeconds = 30,
        int completedOffsetSeconds = 0,
        string name = "Test User")
    {
        var completed = BaseTime.AddSeconds(completedOffsetSeconds);
        return new LeaderboardCandidate(
            attemptId,
            participantId,
            name,
            completed.AddSeconds(-durationSeconds),
            completed,
            correct,
            points,
            MaxPoints: 20,
            TotalQuestions: 10);
    }

    [Fact]
    public void Three_attempts_from_one_participant_contribute_only_the_best_attempt()
    {
        var ranked = LeaderboardRanking.Rank(new[]
        {
            Candidate(1, 10, points: 8, correct: 6),
            Candidate(2, 10, points: 17, correct: 9),
            Candidate(3, 10, points: 12, correct: 7),
            Candidate(4, 20, points: 15, correct: 8)
        });

        Assert.Equal(2, ranked.Count);
        Assert.Equal(2, ranked[0].Candidate.AttemptId);
        Assert.Equal(10, ranked[0].Candidate.ParticipantId);
        Assert.Single(ranked, item => item.Candidate.ParticipantId == 10);
    }

    [Fact]
    public void Points_are_the_primary_sort_key()
    {
        var ranked = LeaderboardRanking.Rank(new[]
        {
            Candidate(1, 1, points: 9, correct: 10, durationSeconds: 1),
            Candidate(2, 2, points: 10, correct: 1, durationSeconds: 100)
        });

        Assert.Equal(2, ranked[0].Candidate.ParticipantId);
    }

    [Fact]
    public void Correct_answers_break_equal_points()
    {
        var ranked = LeaderboardRanking.Rank(new[]
        {
            Candidate(1, 1, points: 10, correct: 6, durationSeconds: 1),
            Candidate(2, 2, points: 10, correct: 7, durationSeconds: 100)
        });

        Assert.Equal(2, ranked[0].Candidate.ParticipantId);
    }

    [Fact]
    public void Shorter_duration_breaks_equal_points_and_correct_answers()
    {
        var ranked = LeaderboardRanking.Rank(new[]
        {
            Candidate(1, 1, points: 10, correct: 7, durationSeconds: 31),
            Candidate(2, 2, points: 10, correct: 7, durationSeconds: 30)
        });

        Assert.Equal(2, ranked[0].Candidate.ParticipantId);
    }

    [Fact]
    public void Completion_time_then_attempt_id_make_ties_deterministic()
    {
        var ranked = LeaderboardRanking.Rank(new[]
        {
            Candidate(30, 3, points: 10, correct: 7, completedOffsetSeconds: 1),
            Candidate(20, 2, points: 10, correct: 7),
            Candidate(10, 1, points: 10, correct: 7)
        });

        Assert.Equal(new[] { 1, 2, 3 }, ranked.Select(item => item.Rank));
        Assert.Equal(new[] { 10, 20, 30 }, ranked.Select(item => item.Candidate.AttemptId));
    }

    [Fact]
    public void Negative_corrupt_duration_is_clamped_to_zero()
    {
        var candidate = new LeaderboardCandidate(
            1, 1, "Test User", BaseTime.AddSeconds(1), BaseTime, 1, 1, 20, 10);

        Assert.Equal(0, candidate.DurationTicks);
    }
}
