# Frontend (Cingiz)

Bu budaqda frontend boşdur. Öz React tətbiqinizi bu qovluğa yerləşdirin.

Backend dəyişdirilməyib: `backend/` qovluğundakı ASP.NET Core API-dən istifadə edin.

## Backend-i işə salmaq

```
cd backend/LiteratureMillionaire.API
dotnet run --launch-profile http
```

API: `http://localhost:5169` · Swagger: `http://localhost:5169/swagger`

CORS `http://localhost:5173` üçün açıqdır (`appsettings.json` → `Cors:AllowedOrigins`).
Başqa portda işləyəcəksinizsə, həmin sətri dəyişin.

## API müqaviləsi

### GET /api/campaigns/current
Ayın kitabı kampaniyası. Xəta halları ProblemDetails + sabit `code`:
`NO_ACTIVE_CAMPAIGN` (404), `MULTIPLE_ACTIVE_CAMPAIGNS` (500).

```json
{
  "campaignId": 1,
  "startDate": "2026-09-01",
  "endDate": "2026-09-30",
  "passingScore": 8,
  "rewardTitle": "...",
  "questionCount": 10,
  "book": { "id": 1, "title": "...", "author": "...", "description": "...", "coverImageUrl": "/covers/oluler.webp" }
}
```

### POST /api/game/start
Yeni oyun: kampaniya kitabından 10 təsadüfi sual. Cavab variantları hər sessiya üçün
serverdə qarışdırılır. Xətalar: 404 `NO_ACTIVE_CAMPAIGN`, 409 `INSUFFICIENT_CAMPAIGN_QUESTIONS`.

```json
{
  "sessionId": "guid",
  "questionNumber": 1,
  "totalQuestions": 10,
  "passingScore": 8,
  "secondsPerQuestion": 15,
  "questionExpiresAtUtc": "2026-09-12T10:00:15Z",
  "question": {
    "id": 12, "text": "...",
    "optionA": "...", "optionB": "...", "optionC": "...", "optionD": "...",
    "difficulty": "Easy", "category": "Ölülər",
    "imageUrl": null, "imageAltText": null
  }
}
```

### POST /api/game/{sessionId}/answer
Gövdə: `{ "questionId": 12, "selectedOption": "A" }`

### POST /api/game/{sessionId}/timeout
Gövdə: `{ "questionId": 12 }`. Vaxt bitməmişdən əvvəl çağırılsa 409 `QUESTION_TIME_REMAINING`.

Hər ikisi eyni cavabı qaytarır. **Doğruluq haqqında heç nə açıqlanmır** — nə düzgün cavab,
nə izah, nə cari bal. Yalnız 10-cu sualdan sonra `result` gəlir:

```json
{
  "questionNumber": 1,
  "timedOut": false,
  "isGameOver": false,
  "nextQuestionNumber": 2,
  "nextQuestion": { "...": "GameQuestionDto" },
  "nextQuestionExpiresAtUtc": "2026-09-12T10:00:30Z",
  "result": null
}
```

Oyun bitəndə:

```json
{ "result": { "correctAnswers": 8, "totalQuestions": 10, "passingScore": 8, "passed": true, "rewardTitle": "..." } }
```

`rewardTitle` yalnız `passed: true` olanda gəlir.

### Digər
- `GET /api/books` — id, title, author, isActive
- `GET|POST|PUT|DELETE /api/questions` — admin CRUD (süzgəclər: `difficulty`, `category`, `bookId`)

## Vacib qaydalar

1. **Vaxtı server müəyyən edir.** `questionExpiresAtUtc` əsas götürülür; öz 15 saniyəlik
   taymerinizi başlatmayın. Səhifə yenilənəndə vaxt sıfırlanmamalıdır.
2. Vaxt bitəndə düymələri kilidləyin və `timeout` endpointini **bir dəfə** çağırın.
3. Aktiv oyunda düzgün/səhv göstərməyin — nəticə yalnız sonda açıqlanır.
4. Kiosk: 1920x1080, toxunma ekranı, sürüşmə olmadan, böyük düymələr, hover tələb etmədən.
5. Sual şəkli varsa yalnız `<img>` ilə göstərin (`/question-images/...`), `object-fit: contain`.

## Verilənlər bazası

Development-də LocalDB avtomatik miqrasiya olunur və seed edilir:
1 kitab ("Ölülər"), 30 təsdiqlənmiş sual, 1 aktiv kampaniya.
