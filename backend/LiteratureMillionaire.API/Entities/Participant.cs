namespace LiteratureMillionaire.API.Entities;

/// <summary>A kiosk visitor identified by mobile number. No authentication; identity is self-declared.</summary>
public class Participant
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;

    /// <summary>Always "+994" followed by nine digits (see PhoneNumber.TryNormalize). Unique.</summary>
    public string NormalizedPhoneNumber { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<QuizAttempt> Attempts { get; set; } = new List<QuizAttempt>();
}
