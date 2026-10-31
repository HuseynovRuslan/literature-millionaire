using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>Uploading pictures for questions and book covers, and listing what has been uploaded.</summary>
[ApiController]
[Route("api/admin/uploads")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminUploadsController : ControllerBase
{
    private readonly IImageUploadService _uploads;

    public AdminUploadsController(IImageUploadService uploads) => _uploads = uploads;

    public sealed record UploadedImageDto(int Id, string Kind, string Url, int Width, int Height, long Bytes,
        string OriginalFileName, DateTime UploadedAtUtc, string UploadedBy, bool AlreadyExisted);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UploadedImageDto>>> List([FromQuery] string? kind, [FromQuery] int take = 60,
        CancellationToken ct = default)
    {
        var images = await _uploads.ListAsync(kind, take, ct);
        return Ok(images.Select(i => new UploadedImageDto(i.Id, i.Kind, i.Url, i.Width, i.Height, i.Bytes,
            i.OriginalFileName, i.UploadedAtUtc, i.UploadedBy, AlreadyExisted: false)).ToList());
    }

    /// <summary>Stores one picture, re-encoded by the server (see <see cref="ImageUploadService"/>).</summary>
    [HttpPost]
    // A little over the service's own limit, so an oversized file is answered by the service's message
    // rather than by the pipeline cutting the connection.
    [RequestSizeLimit(ImageUploadService.MaxBytes + (1024 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = ImageUploadService.MaxBytes + (1024 * 1024))]
    [ProducesResponseType(typeof(UploadedImageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, [FromForm] string? kind, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Refused("EMPTY_FILE", "Şəkil seçilməyib.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var stored = await _uploads.StoreAsync(stream, kind ?? string.Empty, file.FileName, AdminAuth.Actor(User), ct);
            var image = stored.Image;
            return StatusCode(StatusCodes.Status201Created, new UploadedImageDto(image.Id, image.Kind, image.Url,
                image.Width, image.Height, image.Bytes, image.OriginalFileName, image.UploadedAtUtc, image.UploadedBy,
                stored.AlreadyExisted));
        }
        catch (ImageUploadException ex)
        {
            return Refused(ex.Code, ex.Message);
        }
    }

    private ObjectResult Refused(string code, string message)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest,
            "Şəkil qəbul olunmadı.", detail: message);
        problem.Extensions["code"] = code;
        return BadRequest(problem);
    }
}
