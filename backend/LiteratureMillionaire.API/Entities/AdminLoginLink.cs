namespace LiteratureMillionaire.API.Entities;

/// <summary>
/// A break-glass sign-in link for the admin panel, for when QRLog itself is down.
///
/// Created only from a shell on the server (<c>--admin-link</c>), so reaching one already requires server
/// access. Stored in the database rather than the API's memory because the command runs in a separate
/// process. Only a SHA-256 hash of the token is kept: a copy of this table signs nobody in. Single use,
/// short-lived, and still subject to the admin list when redeemed.
/// </summary>
public class AdminLoginLink
{
    public int Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
}
