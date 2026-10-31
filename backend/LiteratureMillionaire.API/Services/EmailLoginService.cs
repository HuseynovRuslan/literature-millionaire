using System.Net;
using System.Security.Cryptography;
using System.Text;
using LiteratureMillionaire.API.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace LiteratureMillionaire.API.Services;

/// <summary>
/// "Email ilə daxil ol": the way in for somebody who is not in QRLog.
///
/// 1. The player types a name and an address. A six-digit code is mailed to that address, and the browser gets
///    back a <b>login id</b> - the code alone is not enough, it has to be presented by the browser that asked.
/// 2. The player types the code. If it matches, the server mints the same kind of <b>sign-in ticket</b> a QRLog
///    sign-in ends with (<see cref="IQrLoginService.IssueTicket"/>), and the quiz is started with it.
///
/// What the code proves is that the player can read that mailbox, so the address is the identity: one participant
/// and one attempt per address, the way QRLog players have one per phone. The name is what the player typed.
///
/// What it guards against: guessing (a code dies after <see cref="MaxWrongCodes"/> wrong tries, and a new one costs
/// a mail), and using this to flood somebody's inbox (one code a minute and <see cref="MaxCodesPerHour"/> an hour
/// per address, on top of nginx's per-client limit). Everything lives in memory and expires on its own; an API
/// restart only means asking for a new code.
/// </summary>
public interface IEmailLoginService
{
    /// <summary>Whether codes can be sent at all. Off means the registration screen offers only QRLog.</summary>
    bool IsEnabled { get; }

    /// <summary>Mails a code. Asking again for the same address replaces the earlier code.</summary>
    /// <exception cref="EmailLoginException"/>
    Task<EmailLoginStartedDto> StartAsync(string? fullName, string? email, CancellationToken ct = default);

    /// <summary>Checks a code and, when it matches, returns the ticket the quiz is started with. A code works once.</summary>
    /// <exception cref="EmailLoginException"/>
    EmailLoginVerifiedDto Verify(string loginId, string? code);
}

/// <summary>Why a code was not sent or not accepted, in the player's words, with a stable code for the screen.</summary>
public sealed class EmailLoginException(int statusCode, string code, string message, int? retryAfterSeconds = null, int? attemptsLeft = null)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public int? RetryAfterSeconds { get; } = retryAfterSeconds;
    public int? AttemptsLeft { get; } = attemptsLeft;
}

public sealed class EmailLoginService : IEmailLoginService
{
    /// <summary>Long enough to switch to the mail app, find the message and come back.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    /// <summary>How long before the same address can be sent another code.</summary>
    public static readonly TimeSpan ResendAfter = TimeSpan.FromSeconds(60);

    public const int MaxCodesPerHour = 5;
    public const int MaxWrongCodes = 5;
    public const int NameMinLength = 2;
    public const int NameMaxLength = 120;

    private const int LoginIdBytes = 16;

    private readonly IMemoryCache _cache;
    private readonly IEmailSender _sender;
    private readonly IQrLoginService _tickets;
    private readonly ILogger<EmailLoginService> _logger;

    /// <summary>Held while deciding whether an address may be sent a code, so two quick requests cannot both pass.</summary>
    private readonly object _gate = new();

    public EmailLoginService(IMemoryCache cache, IEmailSender sender, IQrLoginService tickets, ILogger<EmailLoginService> logger)
    {
        _cache = cache;
        _sender = sender;
        _tickets = tickets;
        _logger = logger;
    }

    public bool IsEnabled => _sender.IsConfigured;

    private sealed class PendingLogin
    {
        public required string Email { get; init; }
        public required string FullName { get; init; }
        public required string Code { get; init; }
        public required DateTime SentAtUtc { get; init; }
        public int WrongCodes { get; set; }
        public bool Spent { get; set; }
    }

    /// <summary>When codes were sent to one address in the last hour.</summary>
    private sealed class SendLog
    {
        public List<DateTime> SentAtUtc { get; } = [];
    }

    public async Task<EmailLoginStartedDto> StartAsync(string? fullName, string? email, CancellationToken ct = default)
    {
        var name = fullName?.Trim() ?? string.Empty;
        if (name.Length is < NameMinLength or > NameMaxLength)
        {
            throw new EmailLoginException(StatusCodes.Status400BadRequest, "INVALID_NAME", "Ad və soyadı yazın.");
        }

        if (!EmailAddress.TryNormalize(email, out var address))
        {
            throw new EmailLoginException(StatusCodes.Status400BadRequest, "INVALID_EMAIL", "Email ünvanı düzgün deyil.");
        }

        if (!IsEnabled)
        {
            throw new EmailLoginException(StatusCodes.Status503ServiceUnavailable, "EMAIL_LOGIN_DISABLED",
                "Email ilə giriş hazırda işləmir. QRLog ilə daxil olun.");
        }

        var loginId = Convert.ToHexString(RandomNumberGenerator.GetBytes(LoginIdBytes)).ToLowerInvariant();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var now = DateTime.UtcNow;

        lock (_gate)
        {
            if (_cache.Get<string>(LatestKey(address)) is { } previousId && _cache.Get<PendingLogin>(LoginKey(previousId)) is { } previous)
            {
                var wait = ResendAfter - (now - previous.SentAtUtc);
                if (wait > TimeSpan.Zero)
                {
                    var seconds = (int)Math.Ceiling(wait.TotalSeconds);
                    throw new EmailLoginException(StatusCodes.Status429TooManyRequests, "EMAIL_CODE_COOLDOWN",
                        $"Kod artıq göndərilib. Yenisini {seconds} saniyə sonra istəyə bilərsiniz.", retryAfterSeconds: seconds);
                }
            }

            var log = _cache.Get<SendLog>(SendsKey(address)) ?? new SendLog();
            log.SentAtUtc.RemoveAll(t => now - t >= TimeSpan.FromHours(1));
            if (log.SentAtUtc.Count >= MaxCodesPerHour)
            {
                var seconds = (int)Math.Ceiling((TimeSpan.FromHours(1) - (now - log.SentAtUtc[0])).TotalSeconds);
                throw new EmailLoginException(StatusCodes.Status429TooManyRequests, "EMAIL_CODE_LIMIT",
                    "Bu ünvana çox kod göndərilib. Bir az sonra yenidən cəhd edin və ya QRLog ilə daxil olun.",
                    retryAfterSeconds: Math.Max(1, seconds));
            }

            log.SentAtUtc.Add(now);
            _cache.Set(SendsKey(address), log, TimeSpan.FromHours(1));

            // One live code per address: the newest is the one in the inbox's top message, so it is the one that works.
            if (_cache.Get<string>(LatestKey(address)) is { } replaced)
            {
                _cache.Remove(LoginKey(replaced));
            }

            _cache.Set(LoginKey(loginId), new PendingLogin { Email = address, FullName = name, Code = code, SentAtUtc = now }, CodeLifetime);
            _cache.Set(LatestKey(address), loginId, CodeLifetime);
        }

        try
        {
            await _sender.SendAsync(Compose(address, name, code), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The code never reached anyone, so it must not be guessable either, and the player may ask again at
            // once. The hourly count stands: a mail server that keeps failing should not be hammered. No address
            // and no exception message in the log - the latter can quote the recipient back.
            lock (_gate)
            {
                _cache.Remove(LoginKey(loginId));
                if (_cache.Get<string>(LatestKey(address)) == loginId) _cache.Remove(LatestKey(address));
            }

            _logger.LogWarning("Email sign-in code could not be sent ({ExceptionType}).", ex.GetType().Name);
            throw new EmailLoginException(StatusCodes.Status502BadGateway, "EMAIL_SEND_FAILED",
                "Kodu göndərmək alınmadı. Bir az sonra yenidən cəhd edin və ya QRLog ilə daxil olun.");
        }

        return new EmailLoginStartedDto(loginId, address, (int)CodeLifetime.TotalSeconds, (int)ResendAfter.TotalSeconds);
    }

    public EmailLoginVerifiedDto Verify(string loginId, string? code)
    {
        var id = loginId?.Trim().ToLowerInvariant() ?? string.Empty;
        if (id.Length == 0 || _cache.Get<PendingLogin>(LoginKey(id)) is not { } pending)
        {
            throw Expired();
        }

        var given = new string((code ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
        lock (pending)
        {
            if (pending.Spent)
            {
                throw Expired();
            }

            if (given.Length == pending.Code.Length
                && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(given), Encoding.ASCII.GetBytes(pending.Code)))
            {
                pending.Spent = true;
                Forget(id, pending);
                var ticket = _tickets.IssueTicket(new SignedInIdentity(pending.FullName, PhoneNumber: null, Email: pending.Email));
                return new EmailLoginVerifiedDto(pending.FullName, pending.Email, ticket);
            }

            pending.WrongCodes += 1;
            var left = MaxWrongCodes - pending.WrongCodes;
            if (left <= 0)
            {
                pending.Spent = true;
                Forget(id, pending);
                throw new EmailLoginException(StatusCodes.Status429TooManyRequests, "TOO_MANY_ATTEMPTS",
                    "Kod bir neçə dəfə səhv yazıldı. Yeni kod istəyin.");
            }

            throw new EmailLoginException(StatusCodes.Status400BadRequest, "CODE_INVALID",
                $"Kod düzgün deyil. {left} cəhdiniz qaldı.", attemptsLeft: left);
        }
    }

    private void Forget(string loginId, PendingLogin pending)
    {
        _cache.Remove(LoginKey(loginId));
        lock (_gate)
        {
            if (_cache.Get<string>(LatestKey(pending.Email)) == loginId) _cache.Remove(LatestKey(pending.Email));
        }
    }

    private static EmailLoginException Expired() =>
        new(StatusCodes.Status404NotFound, "LOGIN_NOT_FOUND", "Kodun vaxtı bitib və ya artıq istifadə olunub. Yeni kod istəyin.");

    private static EmailMessage Compose(string address, string name, string code)
    {
        var minutes = (int)CodeLifetime.TotalMinutes;
        var text =
            $"Salam, {name}!\n\n" +
            $"Kitabxana 2.0 bilik yarışına giriş üçün təsdiq kodunuz: {code}\n\n" +
            $"Kod {minutes} dəqiqə etibarlıdır. Bu kodu siz istəməmisinizsə, məktubu nəzərə almayın.\n\n" +
            "Kitabxana 2.0 — Bilik yarışı";
        var html =
            "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:16px;color:#1b1640;line-height:1.5\">" +
            $"<p>Salam, {WebUtility.HtmlEncode(name)}!</p>" +
            "<p>Kitabxana 2.0 bilik yarışına giriş üçün təsdiq kodunuz:</p>" +
            $"<p style=\"font-size:32px;font-weight:bold;letter-spacing:6px;margin:16px 0\">{code}</p>" +
            $"<p>Kod {minutes} dəqiqə etibarlıdır. Bu kodu siz istəməmisinizsə, məktubu nəzərə almayın.</p>" +
            "<p style=\"color:#6b6790\">Kitabxana 2.0 — Bilik yarışı</p>" +
            "</div>";
        return new EmailMessage(address, $"{code} — Kitabxana 2.0 təsdiq kodu", text, html);
    }

    private static string LoginKey(string loginId) => $"email-login:{loginId}";

    private static string LatestKey(string email) => $"email-login-latest:{email}";

    private static string SendsKey(string email) => $"email-login-sends:{email}";
}
