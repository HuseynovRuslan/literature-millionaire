using LiteratureMillionaire.API.Services;
using Npgsql;

namespace LiteratureMillionaire.Tests;

public class PostgresErrorsTests
{
    private const string ParticipantPhoneUniqueIndex = "IX_Participants_NormalizedPhoneNumber";
    private const string AttemptUniqueIndex = "IX_QuizAttempts_ParticipantId_CampaignId_AttemptNumber";

    private static PostgresException MakeException(string sqlState, string? constraintName = null, string messageText = "boom") =>
        new(messageText, "ERROR", "ERROR", sqlState, constraintName: constraintName);

    [Fact]
    public void Unique_violation_sql_state_is_23505() =>
        Assert.True(PostgresErrors.IsUniqueViolation("23505"));

    [Theory]
    [InlineData("23503")] // foreign_key_violation
    [InlineData("23514")] // check_violation
    [InlineData("40P01")] // deadlock_detected
    [InlineData(null)]    // not a PostgreSQL error at all
    public void Other_sql_states_are_not_unique_violations(string? sqlState) =>
        Assert.False(PostgresErrors.IsUniqueViolation(sqlState));

    [Fact]
    public void Sql_state_is_read_from_the_innermost_PostgresException_in_the_chain()
    {
        var pg = MakeException("23505", ParticipantPhoneUniqueIndex);
        var wrapped = new InvalidOperationException("outer", new TimeoutException("middle", pg));

        Assert.Equal("23505", PostgresErrors.GetSqlState(wrapped));
        Assert.Equal(ParticipantPhoneUniqueIndex, PostgresErrors.GetConstraintName(wrapped));
    }

    [Fact]
    public void Sql_state_and_constraint_name_are_null_when_no_PostgresException_is_in_the_chain()
    {
        var wrapped = new InvalidOperationException("outer", new TimeoutException("inner"));

        Assert.Null(PostgresErrors.GetSqlState(wrapped));
        Assert.Null(PostgresErrors.GetConstraintName(wrapped));
    }

    [Fact]
    public void Unique_violation_on_the_expected_constraint_is_a_race()
    {
        var ex = MakeException("23505", ParticipantPhoneUniqueIndex);

        Assert.True(PostgresErrors.IsUniqueViolationOn(ex, ParticipantPhoneUniqueIndex));
    }

    [Fact]
    public void Unique_violation_on_a_different_constraint_is_not_the_expected_race()
    {
        // A 23505 exists, but it names some other unique index - e.g. the attempt index firing
        // while the caller only expected to contend on the participant phone index.
        var ex = MakeException("23505", AttemptUniqueIndex);

        Assert.False(PostgresErrors.IsUniqueViolationOn(ex, ParticipantPhoneUniqueIndex));
    }

    [Fact]
    public void Unique_violation_with_no_constraint_name_is_not_treated_as_a_race()
    {
        var ex = MakeException("23505", constraintName: null);

        Assert.False(PostgresErrors.IsUniqueViolationOn(ex, ParticipantPhoneUniqueIndex));
    }

    [Fact]
    public void Non_unique_violation_on_the_expected_constraint_name_is_still_not_a_race()
    {
        // Same constraint name, but not a unique_violation - must not be blindly accepted as a race.
        var ex = MakeException("23514", ParticipantPhoneUniqueIndex);

        Assert.False(PostgresErrors.IsUniqueViolationOn(ex, ParticipantPhoneUniqueIndex));
    }

    [Fact]
    public void Non_PostgresException_is_never_a_race() =>
        Assert.False(PostgresErrors.IsUniqueViolationOn(new InvalidOperationException("boom"), ParticipantPhoneUniqueIndex));
}
