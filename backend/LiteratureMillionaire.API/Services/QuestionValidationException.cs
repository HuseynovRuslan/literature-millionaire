namespace LiteratureMillionaire.API.Services;

/// <summary>
/// A question write failed a rule that needs the database to check (for example a
/// BookId that does not exist). The controller turns it into a standard 400
/// validation problem keyed by <see cref="Field"/>.
/// </summary>
public class QuestionValidationException : Exception
{
    public string Field { get; }

    public QuestionValidationException(string field, string message) : base(message)
    {
        Field = field;
    }
}
