namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// One photograph of a <see cref="Plant"/>, with the attribution its licence requires.
/// A plant has exactly one primary photograph and may have further ones; a quiz question shows one of
/// them, chosen per session. Extra photographs are never a separate plant or answer option.
/// </summary>
public class PlantImage
{
    public const int FileNameMaxLength = 60;
    public const int ImageUrlMaxLength = 200;
    public const int SourceMaxLength = 120;
    public const int UrlMaxLength = 500;
    public const int AuthorMaxLength = 200;
    public const int LicenseMaxLength = 120;
    public const int SpecimenSpeciesMaxLength = 160;
    public const int OriginalLabelMaxLength = 300;

    public int Id { get; set; }

    public int PlantId { get; set; }
    public Plant Plant { get; set; } = null!;

    /// <summary>File name as delivered ("plant-001.webp"). Unique; the import matches on it.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Public path of the asset, e.g. "/images/plants/plant-001.webp".</summary>
    public string ImageUrl { get; set; } = string.Empty;

    /// <summary>The plant's main photograph. Exactly one per plant.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Order within the plant's photographs; the primary one comes first.</summary>
    public int DisplayOrder { get; set; }

    // --- attribution, shown in the public "image sources" section -------------------------------

    /// <summary>Where the photograph came from (Wikimedia Commons, iNaturalist, the greenhouse report, ...).</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Page the photograph was taken from, when the source publishes one.</summary>
    public string? SourceUrl { get; set; }

    public string? Author { get; set; }

    /// <summary>Licence as recorded in the catalogue ("CC BY-SA 4.0", "CC0", ...).</summary>
    public string License { get; set; } = string.Empty;

    public string? LicenseUrl { get; set; }

    /// <summary>Species actually shown, when the catalogue answer is a genus or a wider name.</summary>
    public string? SpecimenSpecies { get; set; }

    /// <summary>Caption the photograph carried in its source, when it had one.</summary>
    public string? OriginalLabel { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
