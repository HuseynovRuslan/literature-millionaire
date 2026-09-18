# Signing in to Kitabxana from the same phone

**Done on both sides, 2026-09-17.** QRLog's approval page is live (AttendanceQR PR #3, merged as
`fc749b1`) and Kitabxana points at it: `QRLOG_APP_CONFIRM_URL=https://app.qrlog.az/kitabxana?code={code}`.
What follows is the contract the two sides now keep - read it before changing either half.

Written for: whoever works on the QRLog (AttendanceQR) side.

## The problem

Kitabxana (book.qrlog.az) signs people in by showing a QR that the QRLog app scans. That works when the QR is
on one screen and QRLog is on another device. It cannot work when both are the same phone: a phone cannot
photograph its own screen. Anyone opening book.qrlog.az on their phone — an administrator opening the panel,
a player opening the game — is stuck looking at a QR nobody can scan.

## The flow we want (like SİMA İmza)

1. On the phone, Kitabxana shows **"QRLog tətbiqi ilə təsdiqlə"** instead of asking for a scan.
2. The button opens QRLog (PWA or app) at an address that carries the sign-in **code**.
3. QRLog knows who the employee is — they are signed in there — and asks them to approve: *"Kitabxana 2.0-a
   giriş təsdiqlənsin?"*
4. On approval, QRLog's **server** confirms the code to Kitabxana exactly as it already does after a scan.
5. Kitabxana's page, still open in the other tab, notices the confirmation within a second or two and continues
   by itself. Sending the person back to Kitabxana afterwards is nice, not required.

## What is already done on the Kitabxana side

- `POST /api/qrlog-login/start` now returns **`appConfirmUrl`**: the configured QRLog address with the code
  substituted, or `null` when it is not configured. The button appears only when it is set, so nothing changes
  in production until QRLog is ready.
- The address is configuration: `QrLog__AppConfirmUrl`, a template containing `{code}`, for example
  `https://app.qrlog.az/kitabxana?code={code}`. It is accepted only if it is **https** and on **qrlog.az**
  (or a subdomain) and contains `{code}` — a mistake here must not be able to hand a live sign-in code to
  another host.
- A sign-in code now lives **5 minutes** (was 2), enough to leave for QRLog and come back.
- The Kitabxana page keeps its pending sign-in while it is in the background or reloaded, and asks the server
  again the moment it is looked at, so an approval given elsewhere is picked up and never lost.
- A confirmed sign-in is delivered to **every** copy of the screen that asks (same answer, same ticket, for as
  long as the code lives), and starting one sets an HttpOnly cookie so a **different window** of the same
  browser — an installed app, a fresh tab QRLog hands the person back to — resumes it (`/api/qrlog-login/resume`)
  instead of minting a new code. Both were real failures on phones: "approved in QRLog, nothing arrived".

## What QRLog added (frontend/src/pages/KitabxanaSignInPage.tsx)

**A page at the agreed address**, `GET https://app.qrlog.az/kitabxana?code=<code>`:

- Requires the employee to be signed in to QRLog (the normal QRLog sign-in if they are not).
- Shows what they are approving: that this is a Kitabxana 2.0 sign-in, with their own name — so approving is a
  decision, not a redirect they did not notice.
- On approval, the **QRLog server** (never the browser) calls the endpoint it already calls after a scan:

```
POST https://book.qrlog.az/api/qrlog-login/confirm
Content-Type: application/json

{
  "code":        "<the code from the query string>",
  "fullName":    "<employee's full name>",
  "phoneNumber": "+994XXXXXXXXX",
  "timestamp":   "<ISO 8601 UTC, round-trip format>",
  "signature":   "<hex HMAC-SHA256 of  code + \"\\n\" + phoneNumber + \"\\n\" + timestamp  with the shared secret>"
}
```

- `204 No Content` means it worked. `400`/`404` mean the code is unknown, already used or expired — tell the
  employee to start again in Kitabxana.
- The shared secret is the same `QRLOG_VOUCH_SECRET` both sides already hold; nothing new to exchange.
- The phone number must be a full Azerbaijani mobile number; Kitabxana normalises `0XXXXXXXXX`,
  `994XXXXXXXXX` and `+994XXXXXXXXX`, and refuses anything else.

**Two rules worth keeping:**

- Confirm only codes that arrive with a `code` parameter from this one flow, and treat the code as a secret in
  transit (https, no logging of the full value): whoever holds a confirmed code becomes that employee in
  Kitabxana for that one sign-in.
- Approval must be an explicit tap. A page that confirms on load would let any link someone is sent sign them
  in to Kitabxana without their noticing.

## Turning it on (already done)

On the Kitabxana server (`deploy/.env`), then recreate the api container:

```
QRLOG_APP_CONFIRM_URL=https://app.qrlog.az/kitabxana?code={code}
```

The button appears on phones and desktops alike; the QR stays for the case where the QRLog app is on a
different device. Clearing the setting removes the button again, which is the way to switch the phone
route off without deploying anything.
