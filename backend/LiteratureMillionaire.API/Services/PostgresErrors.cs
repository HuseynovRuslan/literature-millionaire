using Npgsql;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Classifies PostgreSQL errors behind a failed EF Core save. Only the SQLSTATE and constraint
/// name are extracted - message/detail/where text can echo row values (a duplicate-key error
/// quotes the key), so callers log those two fields and never the exception itself.
/// </summary>
public static class PostgresErrors
{
    /// <summary>23505 (unique_violation), per the PostgreSQL error codes appendix.</summary>
    public static bool IsUniqueViolation(string? sqlState) => sqlState == PostgresErrorCodes.UniqueViolation;

    /// <summary>
    /// True when the failure is a 23505 unique_violation against exactly the named unique index
    /// or constraint - i.e. a resolvable race on that one identity, not some other row's
    /// unrelated uniqueness conflict. A unique violation is only ever treated as a race for the
    /// specific constraint the caller expected to contend on.
    /// </summary>
    public static bool IsUniqueViolationOn(Exception exception, string constraintName)
    {
        var pg = Find(exception);
        return pg is not null && IsUniqueViolation(pg.SqlState) && pg.ConstraintName == constraintName;
    }

    /// <summary>The SQLSTATE of the innermost <see cref="PostgresException"/> in the chain, or null when the failure was not raised by PostgreSQL.</summary>
    public static string? GetSqlState(Exception exception) => Find(exception)?.SqlState;

    /// <summary>
    /// The constraint/index name of the innermost <see cref="PostgresException"/> in the chain, or
    /// null. This is a schema identifier chosen in our own migration, never derived from row
    /// data, so it is always safe to log.
    /// </summary>
    public static string? GetConstraintName(Exception exception) => Find(exception)?.ConstraintName;

    private static PostgresException? Find(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e is PostgresException pg)
            {
                return pg;
            }
        }

        return null;
    }
}
