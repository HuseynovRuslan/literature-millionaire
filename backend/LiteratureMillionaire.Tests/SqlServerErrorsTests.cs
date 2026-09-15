using LiteratureMillionaire.API.Services;

namespace LiteratureMillionaire.Tests;

public class SqlServerErrorsTests
{
    [Theory]
    [InlineData(2601)]
    [InlineData(2627)]
    public void Unique_index_and_unique_constraint_violations_are_races(int number) =>
        Assert.True(SqlServerErrors.IsUniqueViolation(number));

    [Theory]
    [InlineData(547)]    // FK / CHECK constraint
    [InlineData(50000)]  // RAISERROR / THROW
    [InlineData(1205)]   // deadlock victim
    [InlineData(-2)]     // timeout
    [InlineData(null)]   // not a SQL Server error at all
    public void Other_errors_are_not_races(int? number) =>
        Assert.False(SqlServerErrors.IsUniqueViolation(number));

    [Fact]
    public void Error_number_is_null_when_no_SqlException_is_in_the_chain() =>
        Assert.Null(SqlServerErrors.GetErrorNumber(new InvalidOperationException("outer", new TimeoutException("inner"))));
}
