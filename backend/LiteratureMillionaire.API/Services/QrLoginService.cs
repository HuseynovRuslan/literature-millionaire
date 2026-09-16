using System.Security.Cryptography;
using System.Text;
using LiteratureMillionaire.API.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// "QRLog ilə davam et": lets an employee who is already registered in QRLog start a quiz without
/// typing their name and phone at the kiosk.
///
/// The exchange, and why it is shaped this way:
///
/// 1. The kiosk asks for a login. It gets back a <b>code</b>, which goes into the QR on screen, and a
///    <b>poll secret</b>, which does not. A QR on a kiosk screen is visible to the whole room; without
///    the secret, anyone who photographed it could poll for the identity that lands on it.
/// 2. The employee scans the QR with the QRLog app, which is already signed in as them.
/// 3. <b>QRLog's server</b> - not the phone - confirms the code to us, signed with a secret the two
///    services share. The phone never holds that secret, and nobody else can vouch for anyone.
/// 4. The kiosk polls with its secret, gets the name and phone, and starts the quiz with them.
///
/// A code is single use and short-lived, and the identity is delivered exactly once. The phone number
/// is the identity the quiz already runs on, so a QRLog sign-in lands on the same participant as
/// typing it by hand would: it is a shortcut past the keyboard, never past the one-attempt rule.
/// </summary>
public interface IQrLoginService
{
    /// <summary>Opens a login and returns the code for the QR plus the secret the caller polls with.</summary>
    QrLoginStartedDto Start();

    /// <summary>Confirms a code on behalf of QRLog. Returns false when the code is unknown, used or expired.</summary>
    bool Confirm(string code, string fullName, string phoneNumber);

    /// <summary>
    /// The phone as this quiz files players under, or false when it is not one it can use.
    ///
    /// Checked at the door on purpose. A number the quiz cannot read is not a small problem later: the
    /// sign-in looks like it worked, the kiosk shows a name, and the start button then fails - which is
    /// exactly how this first shipped, with QRLog sending the nine national digits it stores.
    /// </summary>
    bool TryNormalizePhone(string phoneNumber, out string normalized);

    /// <summary>What the kiosk sees. Wrong or missing secret reads as "no such login", not as a hint.</summary>
    QrLoginStatusDto? Poll(string code, string pollSecret);

    /// <summary>Verifies the signature QRLog sends with a confirmation.</summary>
    bool IsSignatureValid(string code, string phoneNumber, string timestamp, string signature);
}

public sealed class QrLoginService : IQrLoginService
{
    /// <summary>Long enough that a kiosk QR cannot be guessed, short enough to scan reliably.</summary>
    private const int CodeBytes = 16;

    /// <summary>A login is meant to be scanned now, not carried away.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>How far QRLog's clock may be from ours before a confirmation is refused as a replay.</summary>
    private static readonly TimeSpan SignatureWindow = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<QrLoginService> _logger;
    private readonly byte[]? _secret;

    public QrLoginService(IMemoryCache cache, IConfiguration configuration, ILogger<QrLoginService> logger)
    {
        _cache = cache;
        _logger = logger;
        var configured = configuration["QrLog:VouchSecret"];
        _secret = string.IsNullOrWhiteSpace(configured) ? null : Encoding.UTF8.GetBytes(configured);
        if (_secret is null)
        {
            // Not fatal: the rest of the quiz works, the button simply cannot be honoured.
            _logger.LogWarning("QrLog:VouchSecret is not configured; QRLog sign-in will refuse every confirmation.");
        }
    }

    private sealed class PendingLogin
    {
        public required string PollSecret { get; init; }
        public string? FullName { get; set; }
        public string? PhoneNumber { get; set; }
        public bool Confirmed { get; set; }
    }

    public QrLoginStartedDto Start()
    {
        var code = NewToken();
        var pollSecret = NewToken();
        _cache.Set(CacheKey(code), new PendingLogin { PollSecret = pollSecret }, Lifetime);
        return new QrLoginStartedDto(code, pollSecret, DateTime.UtcNow.Add(Lifetime), (int)Lifetime.TotalSeconds);
    }

    public bool TryNormalizePhone(string phoneNumber, out string normalized) =>
        PhoneNumber.TryNormalize(phoneNumber, out normalized);

    public bool Confirm(string code, string fullName, string phoneNumber)
    {
        if (_cache.Get<PendingLogin>(CacheKey(code)) is not { } pending || pending.Confirmed)
        {
            return false;
        }

        pending.FullName = fullName;
        pending.PhoneNumber = phoneNumber;
        pending.Confirmed = true;
        return true;
    }

    public QrLoginStatusDto? Poll(string code, string pollSecret)
    {
        if (_cache.Get<PendingLogin>(CacheKey(code)) is not { } pending)
        {
            return null;
        }

        // Constant-time: a timing difference here would turn the poll into an oracle for the secret.
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(pending.PollSecret), Encoding.UTF8.GetBytes(pollSecret)))
        {
            return null;
        }

        if (!pending.Confirmed)
        {
            return new QrLoginStatusDto("pending", null, null);
        }

        // Delivered once: the kiosk has the identity now, so the code is spent.
        _cache.Remove(CacheKey(code));
        return new QrLoginStatusDto("confirmed", pending.FullName, pending.PhoneNumber);
    }

    public bool IsSignatureValid(string code, string phoneNumber, string timestamp, string signature)
    {
        if (_secret is null)
        {
            return false;
        }

        // A signature is only good for a few minutes, so a captured confirmation cannot be replayed later.
        if (!DateTimeOffset.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sent)
            || (DateTimeOffset.UtcNow - sent).Duration() > SignatureWindow)
        {
            return false;
        }

        var expected = Convert.ToHexString(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes($"{code}\n{phoneNumber}\n{timestamp}")));
        var given = signature.Trim();
        return given.Length == expected.Length
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given.ToUpperInvariant()));
    }

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(CodeBytes)).ToLowerInvariant();

    private static string CacheKey(string code) => $"qrlog-login:{code}";
}
