using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Controllers;

[ApiController]
[Route("api/plants")]
[Produces("application/json")]
public class PlantsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public PlantsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Photograph credits for the plant catalogue: author, source and licence of every picture the quiz
    /// can show. Public and read-only - the licences require the attribution to be visible to visitors.
    /// </summary>
    /// <remarks>
    /// Every catalogue photograph is listed, including those of plants still awaiting an agronomist's
    /// confirmation, because the picture files are published either way. No participant data is involved.
    /// </remarks>
    [HttpGet("credits")]
    [ProducesResponseType(typeof(PlantCreditsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlantCreditsDto>> Credits(CancellationToken ct)
    {
        var images = await _db.PlantImages
            .AsNoTracking()
            .OrderBy(i => i.Plant.Name)
            .ThenBy(i => i.DisplayOrder)
            .Select(i => new PlantImageCreditDto(
                i.Plant.Name,
                i.Plant.ScientificName,
                i.FileName,
                i.ImageUrl,
                i.IsPrimary,
                i.Source,
                i.SourceUrl,
                i.Author,
                i.License,
                i.LicenseUrl,
                i.SpecimenSpecies))
            .ToListAsync(ct);

        var plantCount = await _db.Plants.CountAsync(ct);
        return Ok(new PlantCreditsDto(plantCount, images.Count, images));
    }
}
