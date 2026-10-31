namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// One thing an administrator did, or tried to do: signing in, a refused sign-in, and - as the panel grows -
/// every change to campaigns, books and questions.
///
/// The competition carries a prize, so "who changed the dates of this campaign, and when" has to have an
/// answer that does not depend on anyone's memory. Rows are only ever added; nothing edits or removes them.
/// The actor is stored in full (name and normalised phone): an audit trail that cannot say who did something
/// is not one. The panel shows the phone masked.
/// </summary>
public class AdminAuditEntry
{
    public long Id { get; set; }
    public DateTime AtUtc { get; set; }

    public string ActorName { get; set; } = string.Empty;
    public string ActorPhone { get; set; } = string.Empty;

    /// <summary>A stable, machine-readable verb, e.g. "sign-in", "sign-in-denied", "campaign-updated".</summary>
    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }
    public string? EntityId { get; set; }

    /// <summary>Short human-readable context. Never a secret, never a ticket or a token.</summary>
    public string? Details { get; set; }
}
