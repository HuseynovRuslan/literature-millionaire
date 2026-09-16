namespace LiteratureMillionaire.API.Entities;

/// <summary>Whether a catalogue plant may be used as a live quiz answer.</summary>
public enum PlantQuizStatus
{
    /// <summary>Reviewed and cleared for the live quiz.</summary>
    Approved = 1,

    /// <summary>
    /// Imported but held back ("Şərti" in the reviewed catalogue): the answer name needs an agronomist's
    /// confirmation before it can be shown to players. Never selected for a live quiz until an
    /// administrator moves it to <see cref="Approved"/>.
    /// </summary>
    Conditional = 2,
}

/// <summary>
/// One plant of the recognition catalogue. <see cref="Name"/> is the answer exactly as reviewed
/// (kept verbatim, including names such as "Microphyllus", "Passifloraceae" and "Geran (Pelargonium)").
/// A plant owns one or more photographs; extra photographs never create a second plant or answer.
/// </summary>
public class Plant
{
    public const int CatalogIdMaxLength = 20;
    public const int NameMaxLength = 120;
    public const int ScientificNameMaxLength = 160;
    public const int BotanicalNoteMaxLength = 500;

    public int Id { get; set; }

    /// <summary>Catalogue identifier from the reviewed source ("PLT-001"). Unique; the import matches on it.</summary>
    public string CatalogId { get; set; } = string.Empty;

    /// <summary>Answer shown to the player, exactly as reviewed. Unique across the catalogue.</summary>
    public string Name { get; set; } = string.Empty;

    public string ScientificName { get; set; } = string.Empty;

    public PlantQuizStatus QuizStatus { get; set; } = PlantQuizStatus.Conditional;

    /// <summary>Reviewer's note (e.g. that the catalogue name is a genus and the photo shows one species).</summary>
    public string? BotanicalNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PlantImage> Images { get; set; } = new List<PlantImage>();
}
