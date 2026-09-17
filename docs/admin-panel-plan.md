# Admin panel — plan

Why: after handover nobody can open next month's campaigns, change the month's book, fix a question or
export winners without a developer. Every campaign ends 2026-09-30.

Built in phases. Each phase is finished, tested, verified in a browser and deployed on its own; nothing
half-built reaches production.

## Decisions (2026-09-17)

| Question | Decision | Consequence |
|---|---|---|
| How do admins sign in? | **QRLog QR** — the same sign-in players use; the confirmed phone must be on the admin list | No passwords. A server-side break-glass link covers QRLog being down. |
| Source of truth for questions | **The panel** | Seeders fill an empty database once and then stop reconciling; banks are edited in the panel and new banks arrive through bulk import. |
| Order | **Phase 1, then 2** | Security first; campaigns must exist before 2026-09-30, or the current ones are extended as a stopgap. |

## What exists today

- `/admin` (AdminQuestionsPage): a kiosk-era question form. Blocked in production by nginx (`/admin` → 404).
- `QuestionsController`: question CRUD with **no authentication**; only nginx keeps it private.
- No authentication in the API at all. No endpoints to create campaigns, books or quiz modes. No uploads.
- Seeders reconcile their banks on every deployment: an edit made in the database is overwritten by the
  next deploy. Must change before a question editor exists (Phase 6).
- `/api/game/start` accepts any name and phone: the QRLog sign-in can be bypassed with one HTTP request.

## Phases

### Phase 1a — Verified sign-in (security)
- A confirmed QRLog sign-in yields a short-lived **sign-in ticket** issued by the server (10 minutes; reusable
  within that time, because it can only ever act as its one person and the one-attempt rule still applies).
- `/api/game/start` requires the ticket; name and phone come from the ticket, never from the request body.
- Rate limiting on sign-in start/poll and game start.

### Phase 1b — Admin session
- Admin list (normalised phones, server configuration). Ticket → admin session cookie (HttpOnly, Secure,
  SameSite=Strict, absolute expiry). Sign-out.
- Break-glass: a one-time sign-in link generated on the server (`docker exec`), for when QRLog is down.
- Audit log table: who, when, what, which record. Every later phase writes to it.
- nginx opens only `/admin` and `/api/admin/*`; every admin endpoint requires the admin policy.
  `QuestionsController` also gets the policy (defence in depth).
- Admin shell: sign-in screen (QR), layout, navigation, sign-out.

### Phase 2 — Campaigns (urgent: 2026-09-30)
List, create, edit (dates, passing score, reward, picture questions per round), enable/disable.
Checks: the bank can build a round (3/4/3 difficulty, picture target); no overlapping campaigns in a mode.

### Phase 3 — Results and participants
Attempts and leaderboard per campaign, Excel export (prize handout), remove test participants, reset attempts.

### Phase 4 — Upload infrastructure
Persistent upload volume, served by nginx; type and size checks; server-side conversion to WebP.

### Phase 5 — Books (Ayın Kitabı)
Create a book (title, author, description, cover upload) and attach it to a campaign.

### Phase 6 — Question editor
Search and filter; picture question form with player preview; the seeders' validation rules (four distinct
options, alt text never names the answer, licence required with a picture). Seeders stop reconciling.

### Phase 7 — Bulk import
Upload Excel/JSON → dry-run validation report → apply.

### Phase 8 — Categories
Title, description, order, active; the "test version" flag moves from frontend code to the database.

### Phase 9 — Handover
Admin guide and technical handover document.

### In parallel (not admin)
Show photo credits (CC licences require attribution): 195 picture questions today.

## Status

| Phase | State |
|---|---|
| 1a | **done** — deployed 2026-09-17 (`bdee4eb`). Verified on production: a bare name+phone start returns 401 SIGN_IN_REQUIRED and writes nothing, a forged ticket 401 SIGN_IN_EXPIRED, QR sign-in opening is limited (429 after the burst), and the real player path (QR → server-signed QRLog confirmation → welcome → start → first question) works in a browser. |
| 1b | **done** — deployed 2026-09-17 (`a0d9fdb`). Verified on production with a synthetic admin phone: without a session `/api/admin/session` and `/api/admin/audit` return 401, a non-GET without the header 400, a forged ticket 401, a forged link 401 LINK_INVALID, and `/api/questions` is 404 from outside. In a browser: QR sign-in → panel → Jurnal shows the entry → sign-out; a phone not on the list is refused with a fresh QR; a break-glass link signs in once, leaves the address bar, and is rejected on reuse. Afterwards the verification rows were deleted and `ADMIN_PHONES` was left empty — nobody can sign in until the real admin phones are set in the server's `deploy/.env`. |
| 2 | **done** — deployed 2026-09-17 (`713cf9d`). Verified on production with a synthetic admin phone: the five campaigns listed with their banks, "Növbəti dövr" of Bilik Dünyası proposed 01.10–31.10.2026, a start moved into September was refused as overlapping #2, a save without changes wrote nothing (campaign rows hashed identical before and after); test sign-ins deleted and `ADMIN_PHONES` emptied again. `/admin/campaigns`: list (running first, attempts, problems in words), create, edit, switch on/off, "Növbəti dövr" (same settings; a campaign ending with its month is followed by the whole next month). Refused: an enabled campaign whose bank cannot fill 3/4/3, an "Ayın Kitabı" campaign without a book, two enabled campaigns of one mode sharing a day, a new campaign already over, more than 366 days, and changing the mode or book of a campaign that has attempts. A switched-off campaign saves as a draft with its problems listed. Every create/edit is audited with what changed. Seeders now recognise a seeded campaign by its book alone, so dates changed in the panel are not re-seeded on the next deployment. Nothing is deleted from the panel. |
| 3 | **done** — deployed 2026-09-17 (`c1565b5`). Verified on production with synthetic numbers: a test player started a quiz in campaign #2 through the real QRLog flow; the panel listed it as unfinished; resetting it at once was refused with the wait time (the admin stayed signed in); 31 minutes later (start time moved back in the database) the reset went through; the same number could then start again; the Excel export opened in openpyxl with the full test number and a frozen header; removing the participant deleted them and their attempt; Jurnal showed each step with its reason. Test audit rows deleted and `ADMIN_PHONES` emptied; production back to 7 participants and 0 attempts. `/admin/results/<campaign>`: totals (started, completed, passed, unfinished), attempts ranked exactly like the public leaderboard, phones masked on screen, search by name (Azerbaijani letters optional) or by digits of the number. "Excel-ə ixrac" downloads an .xlsx with full phone numbers (no-store, recorded as `results-exported`; written without a spreadsheet library, text never becomes a formula). "Cəhdi sıfırla" deletes one attempt so that person can play again; "İştirakçını sil" deletes a participant and their attempts in every campaign (test entries). Both need a reason, are written to the audit trail in the same transaction as the delete (who, their result, the reason), and are refused for an unfinished attempt younger than the game session lifetime (30 min), because a deleted attempt would make that player's result fail to save. |
| 4 | **done** — deployed 2026-09-17 (`de01653`, frontend fix `4bb315c`). Verified on production: a sideways 2400×1600 photograph with camera metadata was stored upright as a 1067×1600 WebP with the metadata gone, nginx served it as image/webp with an immutable cache and nosniff, the same file again was recognised instead of stored twice, a PHP file named .png was refused, and `/uploads/` outside the allowed pattern is 404. `deploy/scripts/backup-uploads.sh` archives the volume (added to the deploy user's cron at 03:35, 14-day retention). Test pictures and audit rows removed afterwards. Caught here: the shared axios client sent uploads as JSON, so the API saw no file — fixed. `/admin/images`: upload a picture (drag or choose), see the library per kind, copy a picture's address. The server never stores what it receives: it decodes with SkiaSharp, turns the picture upright from EXIF, shrinks it (question 1600 px, cover 1200 px), re-encodes to WebP and names the file after the hash of the result — so a file that is not really a picture is refused, camera metadata and anything hidden in the file are gone, and the same picture twice is one file and one row. Limits: 10 MB, at least 200 px a side, at most 50 MP, JPG/PNG/WebP only. Stored on the `uploads` volume (API writes, nginx serves read-only at `/uploads/(covers\|questions)/<32 hex>.webp`, immutable cache), backed up by `deploy/scripts/backup-uploads.sh`. Every upload is audited. |
| 5 | **built** — `/admin/books`: every book with its cover, author, description, question and campaign counts, and links to the campaigns it is played in. Add or edit a book; the cover is chosen from the picture library or uploaded there and then, never typed as an address (the server accepts only `/covers/…` or `/uploads/covers/<32 hex>.webp`). Two books may not share a title, and a book in a campaign people are playing cannot be switched off until that campaign is. Nothing is deleted. The campaign form now offers every book for a mode played per book, so a newly added book can be given its campaign before it has questions. |
| 6 – 9 | not started |
