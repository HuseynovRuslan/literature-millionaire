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
/// 4. The kiosk polls with its secret and gets the name and phone to show, plus a <b>sign-in ticket</b>.
/// 5. The quiz is started with the ticket, and the server takes the name and phone from the ticket.
///
/// Step 5 is the one that makes the sign-in mean something. It used to be "start the quiz with the name
/// and phone the poll returned" - which the server could not tell apart from a name and phone typed into
/// a script, so anyone could play as anyone, or as fifty phone numbers, with one HTTP request each.
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

    /// <summary>
    /// The employee a sign-in ticket stands for, or null when the ticket is unknown or has expired.
    /// Not consumed: within its lifetime a ticket can start a quiz more than once, which is harmless
    /// because it can only ever act as the one person QRLog vouched for, and the one-attempt rule still
    /// applies to them.
    /// </summary>
    SignedInIdentity? ResolveTicket(string ticket);

    /// <summary>
    /// Like <see cref="ResolveTicket"/>, but spends the ticket. Signing in to the admin panel uses this: a
    /// session is worth more than a quiz, so the ticket that opened one cannot open a second.
    /// </summary>
    SignedInIdentity? ConsumeTicket(string ticket);
}

public sealed class QrLoginService : IQrLoginService
{
    /// <summary>Long enough that a kiosk QR cannot be guessed, short enough to scan reliably.</summary>
    private const int CodeBytes = 16;

    /// <summary>
    /// A login is meant to be used now, not carried away. Five minutes rather than two because of the phone
    /// route: there, the employee leaves this page for the QRLog app, approves the sign-in and comes back, and
    /// a code that ran out while they were away would send them round again for nothing.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How long a sign-in ticket lasts: long enough to read the welcome screen and press start, short
    /// enough that a ticket left in a kiosk browser is useless to the next person.
    /// </summary>
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(10);

    /// <summary>A ticket is a bearer credential, so it gets the full strength of a session key.</summary>
    private const int TicketBytes = 32;

    /// <summary>How far QRLog's clock may be from ours before a confirmation is refused as a replay.</summary>
    private static readonly TimeSpan SignatureWindow = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ILogger<QrLoginService> _logger;
    private readonly byte[]? _secret;

    /// <summary>
    /// Where QRLog approves a sign-in when the person is already on their phone (QrLog:AppConfirmUrl, with
    /// {code} in it). There is no QR to scan on a phone - it would be scanning its own screen - so instead the
    /// screen offers a button that opens QRLog, which confirms the code the same way the app does after a scan.
    /// Null until it is configured, and then the button is simply not shown.
    /// </summary>
    private readonly string? _appConfirmUrlTemplate;

    public QrLoginService(IMemoryCache cache, IConfiguration configuration, ILogger<QrLoginService> logger)
    {
        _cache = cache;
        _logger = logger;
        _appConfirmUrlTemplate = ReadAppConfirmUrl(configuration["QrLog:AppConfirmUrl"], logger);
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

        /// <summary>Issued on the first confirmed poll and handed to every later one (see Poll).</summary>
        public string? SignInTicket { get; set; }
    }

    public QrLoginStartedDto Start()
    {
        var code = NewToken();
        var pollSecret = NewToken();
        _cache.Set(CacheKey(code), new PendingLogin { PollSecret = pollSecret }, Lifetime);
        return new QrLoginStartedDto(code, pollSecret, DateTime.UtcNow.Add(Lifetime), (int)Lifetime.TotalSeconds,
            _appConfirmUrlTemplate?.Replace("{code}", Uri.EscapeDataString(code), StringComparison.Ordinal));
    }

    /// <summary>
    /// Accepts the configured address only if it is an https URL on qrlog.az carrying {code}. The code is what
    /// a sign-in turns on, so a typo here must not be able to send it to another host.
    /// </summary>
    private static string? ReadAppConfirmUrl(string? configured, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return null;
        }

        var value = configured.Trim();
        var wellFormed = Uri.TryCreate(value.Replace("{code}", "x", StringComparison.Ordinal), UriKind.Absolute, out var uri)
                         && uri.Scheme == Uri.UriSchemeHttps
                         && (uri.Host == "qrlog.az" || uri.Host.EndsWith(".qrlog.az", StringComparison.OrdinalIgnoreCase))
                         && value.Contains("{code}", StringComparison.Ordinal);
        if (!wellFormed)
        {
            logger.LogError("QrLog:AppConfirmUrl is ignored: it must be an https address on qrlog.az containing {{code}}.");
            return null;
        }

        return value;
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

        // The answer stays the same for as long as the code lives, and the ticket it carries is minted once.
        //
        // It used to be delivered exactly once, and that broke the phone route: QRLog hands the browser back
        // to a page that is often a SECOND copy of the screen that started the sign-in (a tab opened from
        // ours inherits its session storage, and a redirect back lands in whichever tab QRLog was opened in),
        // so two copies polled the same code. Whichever asked first took the identity and the other was told
        // its code had expired - which is why the name never arrived on the game screen and why the panel
        // only worked on the second try. Nothing is given away by answering twice: the poll secret is the
        // proof, it never leaves the browser that started the sign-in, and the code still dies with its five
        // minutes. What is single-use is the ticket's effect - an admin session consumes it (ConsumeTicket).
        pending.SignInTicket ??= IssueTicket(pending);
        return new QrLoginStatusDto("confirmed", pending.FullName, pending.PhoneNumber, pending.SignInTicket);
    }

    /// <summary>Mints the ticket a confirmed sign-in carries, and remembers who it stands for.</summary>
    private string IssueTicket(PendingLogin pending)
    {
        var ticket = Convert.ToHexString(RandomNumberGenerator.GetBytes(TicketBytes)).ToLowerInvariant();
        _cache.Set(TicketKey(ticket), new SignedInIdentity(pending.FullName!, pending.PhoneNumber!), TicketLifetime);
        return ticket;
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

    public SignedInIdentity? ResolveTicket(string ticket) =>
        string.IsNullOrWhiteSpace(ticket) ? null : _cache.Get<SignedInIdentity>(TicketKey(ticket.Trim().ToLowerInvariant()));

    private static string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(CodeBytes)).ToLowerInvariant();

    public SignedInIdentity? ConsumeTicket(string ticket)
    {
        if (ResolveTicket(ticket) is not { } identity)
        {
            return null;
        }

        _cache.Remove(TicketKey(ticket.Trim().ToLowerInvariant()));
        return identity;
    }

    private static string TicketKey(string ticket) => $"qrlog-ticket:{ticket}";

    private static string CacheKey(string code) => $"qrlog-login:{code}";
}
