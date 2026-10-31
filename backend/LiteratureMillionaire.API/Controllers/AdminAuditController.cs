using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Controllers;

/// <summary>The admin audit trail, newest first. Read-only: the trail is never edited from anywhere.</summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = AdminAuth.Policy)]
[Produces("application/json")]
public class AdminAuditController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AdminAuditController(ApplicationDbContext db) => _db = db;

    public sealed record AuditEntryDto(long Id, DateTime AtUtc, string ActorName, string ActorPhone, string Action,
        string? EntityType, string? EntityId, string? Details);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> List([FromQuery] int take = 100, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 500);
        var rows = await _db.AdminAuditEntries.AsNoTracking()
            .OrderByDescending(e => e.Id)
            .Take(take)
            .ToListAsync(ct);

        // The trail keeps the full phone; the screen shows it masked.
        return Ok(rows.Select(e => new AuditEntryDto(e.Id, e.AtUtc, e.ActorName, PhoneNumber.Mask(e.ActorPhone),
            e.Action, e.EntityType, e.EntityId, e.Details)).ToList());
    }
}
