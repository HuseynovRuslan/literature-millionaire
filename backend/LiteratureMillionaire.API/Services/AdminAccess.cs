using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.API.Services;

/// <summary>Names shared by the admin authentication pieces.</summary>
public static class AdminAuth
{
    public const string Scheme = "KitabxanaAdmin";
    public const string Policy = "Admin";
    public const string PhoneClaim = "phone";

    /// <summary>
    /// The header every state-changing admin request must carry. A browser will not add a custom header to a
    /// cross-site request without a CORS preflight this API never grants, so together with the SameSite=Strict
    /// cookie it keeps another site from acting in an admin's name.
    /// </summary>
    public const string CsrfHeader = "X-Kitabxana-Admin";

    public static ClaimsPrincipal Principal(string fullName, string phoneNumber) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, fullName), new Claim(PhoneClaim, phoneNumber)],
            Scheme));

    public static AdminActor Actor(ClaimsPrincipal user) =>
        new(user.FindFirstValue(ClaimTypes.Name) ?? string.Empty, user.FindFirstValue(PhoneClaim) ?? string.Empty);
}

/// <summary>Who did something in the panel.</summary>
public sealed record AdminActor(string FullName, string PhoneNumber);

/// <summary>
/// Who may use the admin panel: normalised phone numbers from configuration (Admin:Phones, separated by commas,
/// semicolons or spaces - Admin__Phones on the server).
///
/// Read on every check rather than once at startup, so taking someone off the list takes effect on their very
/// next request instead of when their eight-hour session happens to end. An empty or missing list means nobody:
/// the panel fails closed.
/// </summary>
public interface IAdminDirectory
{
    bool IsAdmin(string phoneNumber);
}

public sealed class AdminDirectory : IAdminDirectory
{
    private readonly IConfiguration _configuration;

    public AdminDirectory(IConfiguration configuration) => _configuration = configuration;

    public bool IsAdmin(string phoneNumber)
    {
        if (!PhoneNumber.TryNormalize(phoneNumber, out var candidate))
        {
            return false;
        }

        var configured = _configuration["Admin:Phones"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            return false;
        }

        foreach (var entry in configured.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (PhoneNumber.TryNormalize(entry, out var admin) && admin == candidate)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Passes when the signed-in admin's phone is still on the admin list - checked per request, not only at sign-in.
/// </summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminRequirementHandler : AuthorizationHandler<AdminRequirement>
{
    private readonly IAdminDirectory _admins;

    public AdminRequirementHandler(IAdminDirectory admins) => _admins = admins;

    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var phone = context.User.FindFirstValue(AdminAuth.PhoneClaim);
        if (context.User.Identity?.IsAuthenticated == true && phone is not null && _admins.IsAdmin(phone))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Append-only record of what administrators did. See <see cref="AdminAuditEntry"/>.</summary>
public interface IAdminAuditLog
{
    Task RecordAsync(AdminActor actor, string action, string? entityType = null, string? entityId = null,
        string? details = null, CancellationToken ct = default);
}

public sealed class AdminAuditLog : IAdminAuditLog
{
    private readonly ApplicationDbContext _db;

    public AdminAuditLog(ApplicationDbContext db) => _db = db;

    public async Task RecordAsync(AdminActor actor, string action, string? entityType = null, string? entityId = null,
        string? details = null, CancellationToken ct = default)
    {
        _db.AdminAuditEntries.Add(new AdminAuditEntry
        {
            AtUtc = DateTime.UtcNow,
            ActorName = Truncate(actor.FullName, 120),
            ActorPhone = Truncate(actor.PhoneNumber, 16),
            Action = Truncate(action, 64),
            EntityType = entityType is null ? null : Truncate(entityType, 64),
            EntityId = entityId is null ? null : Truncate(entityId, 64),
            Details = details is null ? null : Truncate(details, 2000),
        });
        await _db.SaveChangesAsync(ct);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}

/// <summary>
/// Break-glass sign-in links (see <see cref="AdminLoginLink"/>). Created by <c>--admin-link</c> on the server,
/// redeemed once by the panel.
/// </summary>
public interface IAdminLoginLinks
{
    /// <summary>Creates a link for an admin and returns its token. The token itself is never stored.</summary>
    Task<string> CreateAsync(string phoneNumber, string fullName, CancellationToken ct = default);

    /// <summary>The admin a token signs in, or null when it is unknown, used or expired. Spends the token.</summary>
    Task<AdminActor?> RedeemAsync(string token, CancellationToken ct = default);
}

public sealed class AdminLoginLinks : IAdminLoginLinks
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private const int TokenBytes = 32;

    private readonly ApplicationDbContext _db;

    public AdminLoginLinks(ApplicationDbContext db) => _db = db;

    public async Task<string> CreateAsync(string phoneNumber, string fullName, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();
        var now = DateTime.UtcNow;
        _db.AdminLoginLinks.Add(new AdminLoginLink
        {
            TokenHash = Hash(token),
            PhoneNumber = phoneNumber,
            FullName = fullName,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(Lifetime),
        });
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<AdminActor?> RedeemAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Hash(token.Trim().ToLowerInvariant());
        var now = DateTime.UtcNow;

        // Spend it in the same statement that checks it, so two tabs racing with one link cannot both win.
        var spent = await _db.AdminLoginLinks
            .Where(l => l.TokenHash == hash && l.UsedAtUtc == null && l.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(set => set.SetProperty(l => l.UsedAtUtc, now), ct);
        if (spent != 1)
        {
            return null;
        }

        var link = await _db.AdminLoginLinks.AsNoTracking().SingleAsync(l => l.TokenHash == hash, ct);
        return new AdminActor(link.FullName, link.PhoneNumber);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
