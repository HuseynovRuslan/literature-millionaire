namespace LiteratureMillionaire.API.Dtos;

/// <summary>
/// Public attribution for one catalogue photograph, as shown in the "image sources" section.
/// Carries only what the licences require plus what identifies the picture; no internal ids beyond
/// the ones already visible in the image path. <c>SpecimenSpecies</c> names the species actually
/// pictured, when the catalogue answer is a genus or a wider name.
/// </summary>
public record PlantImageCreditDto(
    string PlantName,
    string ScientificName,
    string FileName,
    string ImageUrl,
    bool IsPrimary,
    string Source,
    string? SourceUrl,
    string? Author,
    string License,
    string? LicenseUrl,
    string? SpecimenSpecies);

/// <summary>The whole credits list, with the counts a reader can check at a glance.</summary>
public record PlantCreditsDto(
    int PlantCount,
    int ImageCount,
    IReadOnlyList<PlantImageCreditDto> Images);
