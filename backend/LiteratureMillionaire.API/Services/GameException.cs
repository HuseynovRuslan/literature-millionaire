namespace LiteratureMillionaire.API.Services;

/// <summary>
/// A game-rule violation that maps directly to an HTTP problem response.
/// Thrown by <see cref="GameService"/>, translated by the controller.
/// </summary>
public class GameException : Exception
{
    public int StatusCode { get; }
    public string Title { get; }
    public IDictionary<string, object?>? Extensions { get; }

    public GameException(int statusCode, string title, string detail, IDictionary<string, object?>? extensions = null)
        : base(detail)
    {
        StatusCode = statusCode;
        Title = title;
        Extensions = extensions;
    }

    public static GameException NotFound(string detail) =>
        new(StatusCodes.Status404NotFound, "Game session not found", detail);

    public static GameException Conflict(string title, string detail, IDictionary<string, object?>? extensions = null) =>
        new(StatusCodes.Status409Conflict, title, detail, extensions);

    /// <summary>500 with a generic detail: the database failed in a way the caller cannot resolve. Nothing from the underlying exception is exposed.</summary>
    public static GameException DatabaseError() =>
        new(StatusCodes.Status500InternalServerError, "Database error",
            "The request could not be completed because of a database error. Please try again.",
            new Dictionary<string, object?> { ["code"] = "DATABASE_ERROR" });
}
