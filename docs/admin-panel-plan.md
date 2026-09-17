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
| 2 | **built** — `/admin/campaigns`: list (running first, attempts, problems in words), create, edit, switch on/off, "Növbəti dövr" (same settings; a campaign ending with its month is followed by the whole next month). Refused: an enabled campaign whose bank cannot fill 3/4/3, an "Ayın Kitabı" campaign without a book, two enabled campaigns of one mode sharing a day, a new campaign already over, more than 366 days, and changing the mode or book of a campaign that has attempts. A switched-off campaign saves as a draft with its problems listed. Every create/edit is audited with what changed. Seeders now recognise a seeded campaign by its book alone, so dates changed in the panel are not re-seeded on the next deployment. Nothing is deleted from the panel. |
| 3 – 9 | not started |
