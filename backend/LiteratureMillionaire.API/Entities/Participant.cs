namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// A player, identified by mobile number (a QRLog sign-in) or by email address (an emailed code) - exactly one of
/// the two, which is what the one-attempt rule counts by.
/// </summary>
public class Participant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Always "+994" followed by nine digits (see PhoneNumber.TryNormalize). Unique. Null for an email player.</summary>
    public string? NormalizedPhoneNumber { get; set; }

    /// <summary>Lower-case, trimmed (see EmailAddress.TryNormalize). Unique. Null for a QRLog player.</summary>
    public string? Email { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
