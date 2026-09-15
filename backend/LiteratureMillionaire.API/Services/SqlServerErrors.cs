using Microsoft.Data.SqlClient;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// Classifies SQL Server errors behind a failed EF Core save. Only the error number is
/// extracted: the message text can echo row values (a duplicate-key error quotes the key),
/// so callers log the number and never the exception itself.
/// </summary>
public static class SqlServerErrors
{
    /// <summary>2601: duplicate key in a unique index; 2627: unique/primary key constraint violation.</summary>
    public static bool IsUniqueViolation(int? sqlErrorNumber) => sqlErrorNumber is 2601 or 2627;

    /// <summary>The SQL Server error number of the innermost <see cref="SqlException"/>, or null when the failure was not raised by SQL Server.</summary>
    public static int? GetErrorNumber(Exception exception)
    {
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            if (e is SqlException sql)
            {
                return sql.Number;
            }
        }

        return null;
    }
}
