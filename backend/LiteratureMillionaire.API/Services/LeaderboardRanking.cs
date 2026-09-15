namespace LiteratureMillionaire.API.Services;

/// <summary>The minimal database projection needed to rank a completed attempt.</summary>
public sealed record LeaderboardCandidate(
    int AttemptId,
    int ParticipantId,
    string FullName,
    DateTime StartedAtUtc,
    DateTime CompletedAtUtc,
    int CorrectAnswers,
    int PointsEarned,
    int MaxPoints,
    int TotalQuestions)
{
    public long DurationTicks => Math.Max(0, CompletedAtUtc.Ticks - StartedAtUtc.Ticks);
}

/// <summary>An internally ranked candidate. Internal identifiers never enter public DTOs.</summary>
public sealed record RankedLeaderboardCandidate(int Rank, LeaderboardCandidate Candidate);

/// <summary>Deterministic best-attempt selection and ranking, independent of persistence.</summary>
public static class LeaderboardRanking
{
    public static IReadOnlyList<RankedLeaderboardCandidate> Rank(IEnumerable<LeaderboardCandidate> candidates)
    {
        var bestPerParticipant = candidates
            .GroupBy(candidate => candidate.ParticipantId)
            .Select(group => Order(group).First());

        return Order(bestPerParticipant)
            .Select((candidate, index) => new RankedLeaderboardCandidate(index + 1, candidate))
            .ToArray();
    }

    private static IOrderedEnumerable<LeaderboardCandidate> Order(IEnumerable<LeaderboardCandidate> candidates) =>
        candidates
            .OrderByDescending(candidate => candidate.PointsEarned)
            .ThenByDescending(candidate => candidate.CorrectAnswers)
            .ThenBy(candidate => candidate.DurationTicks)
            .ThenBy(candidate => candidate.CompletedAtUtc)
            .ThenBy(candidate => candidate.AttemptId);
}
