using System.ComponentModel.DataAnnotations;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>
/// Bulk import: upload a workbook to see what it would do, then apply that report. Uploading writes nothing.
/// </summary>
[ApiController]
[Route("api/admin/questions/import")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminImportController : ControllerBase
{
    private readonly IAdminQuestionImportService _import;

    public AdminImportController(IAdminQuestionImportService import) => _import = import;

    public sealed class ApplyRequest
    {
        [Required, MaxLength(64)]
        public string Token { get; set; } = string.Empty;
    }

    /// <summary>Reads the file and reports, row by row, what would happen. Nothing is written here.</summary>
    [HttpPost]
    [RequestSizeLimit(AdminQuestionImportService.MaxBytes + (1024 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = AdminQuestionImportService.MaxBytes + (1024 * 1024))]
    [ProducesResponseType(typeof(AdminImportReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Analyse([FromForm] IFormFile? file, [FromForm] int bookId, [FromForm] int quizModeId,
        [FromForm] string? category, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return Refused("EMPTY_FILE", "Fayl seçilməyib.");
        }

        try
        {
            await using var stream = file.OpenReadStream();
            return Ok(await _import.AnalyseAsync(stream, file.FileName, bookId, quizModeId, category, AdminAuth.Actor(User), ct));
        }
        catch (AdminImportException ex)
        {
            return Refused(ex.Code, ex.Message);
        }
    }

    /// <summary>Writes the rows the report called ready, in one transaction.</summary>
    [HttpPost("apply")]
    [ProducesResponseType(typeof(AdminImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Apply([FromBody] ApplyRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await _import.ApplyAsync(request.Token, AdminAuth.Actor(User), ct));
        }
        catch (AdminImportException ex)
        {
            return Refused(ex.Code, ex.Message);
        }
    }

    /// <summary>An empty workbook with the columns this understands.</summary>
    [HttpGet("template.xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public IActionResult Template() =>
        File(_import.Template(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sual-numunesi.xlsx");

    private ObjectResult Refused(string code, string message)
    {
        var problem = ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status400BadRequest,
            "Fayl qəbul olunmadı.", detail: message);
        problem.Extensions["code"] = code;
        return BadRequest(problem);
    }
}
