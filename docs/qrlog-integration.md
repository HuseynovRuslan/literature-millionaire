# "QRLog ilə davam et" — the QRLog side

Kitabxana 2.0 (`book.qrlog.az`) lets an employee who is already in QRLog start a quiz by scanning a QR
with the QRLog app, instead of typing their name and phone at the kiosk. **The Kitabxana half is built
and merged.** This is the other half, which lives in the AttendanceQR repository.

It is written out here rather than committed there because that working tree currently has 25
uncommitted changes — eight deleted brand files, a modified landing site, `theme.css` and
`EquipmentPage.tsx`. That is somebody's rebrand in progress, and adding to it risked sweeping this
feature into their commit. Apply this once that work is in.

## How the exchange works

    kiosk                     QRLog app            QRLog server           Kitabxana server
      |                                                                        |
      |-- POST /api/qrlog-login/start ---------------------------------------->|
      |<-- { code, pollSecret, expiresAtUtc } --------------------------------|
      |   shows QR containing https://book.qrlog.az/qr/<code>                  |
      |                                                                        |
      |            scan ----->|                                                |
      |                       |-- POST /api/kitabxana/sign-in { code } ------->|
      |                       |        (employee's own JWT)      |             |
      |                       |                                  |-- POST /api/qrlog-login/confirm
      |                       |                                  |   { code, fullName, phoneNumber,
      |                       |                                  |     timestamp, signature } ---->|
      |                       |<------------------- 204 ---------|             |
      |-- GET /api/qrlog-login/<code>?secret=<pollSecret> -------------------->|
      |<-- { status: "confirmed", fullName, phoneNumber } --------------------|

Three things make the QR safe to show on a screen a whole room can see:

- **The poll secret never leaves the kiosk browser.** The QR carries only the code. Polling without
  the secret returns 404 — the same answer as a code that never existed, so it is not an oracle
  either.
- **QRLog's server confirms, not the phone.** The shared secret stays on two servers. A confirmation
  older than five minutes is refused, so a captured one cannot be replayed.
- **A code is single use and lives two minutes**, and the identity is handed over exactly once.

With no secret configured, Kitabxana refuses every confirmation rather than trusting it.

## What Kitabxana exposes

| Method | Path | Who calls it |
| --- | --- | --- |
| `POST` | `/api/qrlog-login/start` | the kiosk |
| `POST` | `/api/qrlog-login/confirm` | **QRLog's server** |
| `GET` | `/api/qrlog-login/{code}?secret=…` | the kiosk |

`confirm` takes `{ code, fullName, phoneNumber, timestamp, signature }` and answers `204`, `401`
(signature or timestamp) or `409` (code unknown, expired or already used).

**Signature.** Hex-encoded HMAC-SHA256 over the three values joined by `\n`, keyed with the shared
secret:

    signature = HMACSHA256(secret, $"{code}\n{phoneNumber}\n{timestamp}")   // uppercase hex

`timestamp` is ISO 8601 round-trip UTC (`DateTimeOffset.UtcNow.ToString("O")`). The phone is signed
as it is sent, byte for byte.

## 1. The endpoint to add

`src/AttendanceQR.Api/Controllers/KitabxanaController.cs`:

```csharp
using System.Security.Cryptography;
using System.Text;
using AttendanceQR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceQR.Api.Controllers;

/// <summary>
/// Vouches for the signed-in employee to Kitabxana 2.0 (book.qrlog.az), so they can start a quiz
/// without typing their name and phone at the kiosk.
///
/// The employee's JWT is what proves who they are; this endpoint never takes a name or a phone from
/// the caller. The signature that goes out is computed here, on the server, because the shared secret
/// must not exist on a phone.
/// </summary>
[ApiController]
[Route("api/kitabxana")]
[Authorize]
public class KitabxanaController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    private readonly ILogger<KitabxanaController> _logger;

    public KitabxanaController(AppDbContext db, IHttpClientFactory http, IConfiguration config, ILogger<KitabxanaController> logger)
    {
        _db = db;
        _http = http;
        _config = config;
        _logger = logger;
    }

    public sealed record SignInRequest(string Code);

    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn([FromBody] SignInRequest request, CancellationToken ct)
    {
        var secret = _config["Kitabxana:VouchSecret"];
        var baseUrl = _config["Kitabxana:BaseUrl"] ?? "https://book.qrlog.az";
        var allowedTenants = _config.GetSection("Kitabxana:TenantIds").Get<Guid[]>() ?? [];

        // Fail-closed, as everywhere else here: unconfigured means "not for this company", not "for all".
        if (string.IsNullOrWhiteSpace(secret) || allowedTenants.Length == 0)
        {
            return StatusCode(503, new { error = "NotConfigured" });
        }

        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Length is < 8 or > 64)
        {
            return BadRequest(new { error = "InvalidCode" });
        }

        var employeeId = User.EmployeeId();
        var employee = await _db.Employees
            .AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new { e.FullName, e.PhoneNumber, e.TenantId })
            .FirstOrDefaultAsync(ct);

        if (employee is null)
        {
            return Unauthorized(new { error = "UnknownEmployee" });
        }

        // The quiz is one company's. Somebody else's employee must not be signed into it by accident.
        if (!allowedTenants.Contains(employee.TenantId))
        {
            return StatusCode(403, new { error = "NotEligible" });
        }

        // The quiz identifies a player by phone number - that is what its one-attempt rule is keyed on.
        // Without one there is nothing to sign them in as.
        if (string.IsNullOrWhiteSpace(employee.PhoneNumber))
        {
            return BadRequest(new { error = "NoPhoneNumber" });
        }

        var timestamp = DateTimeOffset.UtcNow.ToString("O");
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{request.Code}\n{employee.PhoneNumber}\n{timestamp}")));

        var client = _http.CreateClient("kitabxana");
        client.Timeout = TimeSpan.FromSeconds(10);

        try
        {
            using var response = await client.PostAsJsonAsync($"{baseUrl}/api/qrlog-login/confirm", new
            {
                code = request.Code,
                fullName = employee.FullName,
                phoneNumber = employee.PhoneNumber,
                timestamp,
                signature,
            }, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // The QR on the kiosk has expired or was already used - the player just needs a new one.
                return Conflict(new { error = "CodeExpired" });
            }

            if (!response.IsSuccessStatusCode)
            {
                // Never log the secret, the signature or the employee's phone.
                _logger.LogWarning("Kitabxana sign-in refused for employee {EmployeeId}: {Status}.", employeeId, (int)response.StatusCode);
                return StatusCode(502, new { error = "KitabxanaRefused" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Kitabxana sign-in could not be delivered for employee {EmployeeId} ({ExceptionType}).", employeeId, ex.GetType().Name);
            return StatusCode(502, new { error = "KitabxanaUnreachable" });
        }

        return NoContent();
    }
}
```

Register the client next to the existing one in `Program.cs`:

```csharp
builder.Services.AddHttpClient("kitabxana", c => c.Timeout = TimeSpan.FromSeconds(10));
```

## 2. The scanner

`frontend/src/pages/ScanPage.tsx`, at the very top of `onDecoded` (line ~575) — **before**
`stopCamera()` and the selfie flow, because this is not a check-in and must not photograph anyone:

```ts
async function onDecoded(text: string) {
  if (busyRef.current) return

  // Kitabxana 2.0 sign-in: the kiosk shows https://book.qrlog.az/qr/<code>. Handled here and
  // returned, so the check-in flow - selfie, location, attendance record - never starts for it.
  const kitabxana = /^https?:\/\/book\.qrlog\.az\/qr\/([0-9a-f]{32})$/i.exec(text.trim())
  if (kitabxana) {
    busyRef.current = true
    try {
      await stopCamera()
      setPhase('processing')
      await api.post('/api/kitabxana/sign-in', { code: kitabxana[1] })
      // Tell them to look back at the kiosk: that is where the quiz continues.
      showMessage('Kitabxana 2.0-da daxil oldunuz. Ekrana baxın.')
    } catch {
      showMessage('Giriş alınmadı. QR kodun vaxtı bitmiş ola bilər — ekrandan yeni kod alın.')
    } finally {
      busyRef.current = false
    }
    return
  }

  busyRef.current = true
  // …the existing check-in flow, unchanged from here…
```

`showMessage` stands in for whatever ScanPage already uses to show a result; wire it to the same one.

## 3. Configuration

Generate one secret and put the same value on both sides:

```bash
openssl rand -base64 48
```

**Kitabxana** — `deploy/.env` on the VPS:

    QRLOG_VOUCH_SECRET=<the secret>

**QRLog** — its own configuration:

    Kitabxana__VouchSecret=<the same secret>
    Kitabxana__BaseUrl=https://book.qrlog.az
    Kitabxana__TenantIds__0=<Bakı Abadlıq Xidməti tenant id>

Anyone holding that secret can claim to be any employee, so treat it as a password: not in git, not in
a chat message, rotated if it is ever pasted somewhere it should not be.

## 4. What to check once it is applied

1. Open the kiosk registration screen, press **QRLog ilə davam et**. A QR appears with a countdown.
2. Scan it with the QRLog app while signed in. The kiosk fills in the name and phone within ~1.5 s,
   and the app says to look back at the screen.
3. The kiosk shows **QRLog: \<name\>** and the quiz starts from the ordinary button.
4. Scan the same QR again: the app reports an expired code and the kiosk is unaffected.
5. Wait past two minutes without scanning: the kiosk offers a new QR.
6. Sign in as an employee of another tenant: refused with `NotEligible`.
7. Sign in as an employee with no phone number: refused with `NoPhoneNumber`.
8. Play a quiz through a QRLog sign-in, then try to start a second one for the same campaign: refused
   with `ATTEMPT_LIMIT_REACHED`. Signing in with QRLog is a shortcut past the keyboard, not past the
   one-attempt rule.

The Kitabxana side of all of this is already covered by tests: seven in
`backend/LiteratureMillionaire.Tests/QrLoginApiTests.cs` for the exchange itself, plus an end-to-end
browser run that stands in for QRLog with a signed confirmation.
