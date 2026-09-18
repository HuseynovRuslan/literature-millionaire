using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Bulk import (phase 7). Uploading a file writes nothing: it reports, row by row, what would happen — and the
/// rows it calls ready are the ones applying writes, held to the same rules the editor enforces.
/// </summary>
public class AdminImportTests
{
    [Fact]
    public async Task Importing_needs_an_admin_session_and_the_admin_header()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var anonymous = Https(factory);

        using var upload = await UploadAsync(anonymous, Workbook([Row("Sual?", "A", "B", "C", "D", "A")]), bookId, modeId);
        using var template = await anonymous.GetAsync("/api/admin/questions/import/template.xlsx");
        Assert.Equal(HttpStatusCode.Unauthorized, upload.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, template.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsync("/api/admin/questions/import", Multipart(Workbook([Row("Sual?", "A", "B", "C", "D", "A")]), bookId, modeId));
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task A_file_is_reported_on_before_anything_is_written_and_then_applied_as_reported()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var admin = await SignedInAsync(factory);
        var workbook = Workbook(
        [
            Row("Hansı ağac payızda qırmızı olur?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "B", "Orta", "Bitkilər"),
            Row("Bakının ən qədim qapısı hansıdır?", "Qoşa qala", "Şamaxı", "Salyan", "Gəncə", "A", "Asan", "Şəhər"),
        ]);

        using var upload = await UploadAsync(admin, workbook, bookId, modeId);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var report = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, report.GetProperty("ready").GetInt32());
        Assert.Equal(0, report.GetProperty("problems").GetInt32());
        Assert.Equal("Test Book", report.GetProperty("bankTitle").GetString());
        // Reporting is not writing: nothing has reached the bank yet.
        Assert.Equal(0, await QuestionCountAsync(factory));

        using var applied = await SendAsync(admin, HttpMethod.Post, "/api/admin/questions/import/apply",
            new { token = report.GetProperty("token").GetString() });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        Assert.Equal(2, (await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("added").GetInt32());
        Assert.Equal(2, await QuestionCountAsync(factory));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Questions.AsNoTracking().OrderBy(q => q.Id).ToListAsync();
            // Playable: in the bank, in the category the game draws from, with the answer the file named.
            Assert.All(stored, q => Assert.Equal((bookId, modeId), (q.BookId, q.QuizModeId)));
            Assert.Equal('B', stored[0].CorrectOption);
            Assert.Equal("Bitkilər", stored[0].Category);
            var entry = await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
            Assert.Equal("questions-imported", entry.Action);
            Assert.Contains("2 sual", entry.Details);
        }

        // A report is spent once applied: the same token cannot write the file twice.
        using var again = await SendAsync(admin, HttpMethod.Post, "/api/admin/questions/import/apply",
            new { token = report.GetProperty("token").GetString() });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
        Assert.Equal("REPORT_EXPIRED", (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(2, await QuestionCountAsync(factory));
    }

    [Fact]
    public async Task Rows_that_cannot_be_played_are_named_by_their_row_number_and_left_out()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var admin = await SignedInAsync(factory);
        var workbook = Workbook(
        [
            Row("Yaxşı sual?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "B", "Orta", "Bitkilər"),          // row 2
            Row("Təkrarlanan variant?", "Palıd", "palıd", "Şam", "Sərv", "A", "Orta", "Bitkilər"),      // row 3
            Row("Cavabsız sual?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "X", "Orta", "Bitkilər"),        // row 4
            Row("", "Palıd", "Ağcaqayın", "Şam", "Sərv", "A", "Orta", "Bitkilər"),                      // row 5
            Row("Yaxşı sual?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "B", "Orta", "Bitkilər"),           // row 6: the same one twice
            Row("Tanınmayan çətinlik?", "Palıd", "Ağcaqayın", "Şam", "Sərv", "C", "Çox çətin", "Bitkilər"), // row 7
        ]);

        using var upload = await UploadAsync(admin, workbook, bookId, modeId);
        var report = await upload.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, report.GetProperty("ready").GetInt32());
        Assert.Equal(1, report.GetProperty("duplicates").GetInt32());
        Assert.Equal(4, report.GetProperty("problems").GetInt32());

        var rows = report.GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("row").GetInt32());
        Assert.Equal("ready", rows[2].GetProperty("status").GetString());
        Assert.Contains("fərqli", rows[3].GetProperty("problems")[0].GetString());
        Assert.Contains("Düzgün cavabı seçin", rows[4].GetProperty("problems")[0].GetString());
        Assert.Contains("Sualı yazın", rows[5].GetProperty("problems")[0].GetString());
        Assert.Equal("duplicate", rows[6].GetProperty("status").GetString());
        Assert.Contains("Çətinlik tanınmadı", string.Join(" ", rows[7].GetProperty("problems").EnumerateArray().Select(p => p.GetString())));

        using var applied = await SendAsync(admin, HttpMethod.Post, "/api/admin/questions/import/apply",
            new { token = report.GetProperty("token").GetString() });
        Assert.Equal(1, (await applied.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("added").GetInt32());
        Assert.Equal(1, await QuestionCountAsync(factory));
    }

    [Fact]
    public async Task A_question_already_in_the_bank_is_not_imported_twice()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var admin = await SignedInAsync(factory);
        var workbook = Workbook([Row("Təkrar sual?", "A", "B", "C", "D", "A", "Orta", "Bitkilər")]);

        using var first = await UploadAsync(admin, workbook, bookId, modeId);
        using var appliedFirst = await SendAsync(admin, HttpMethod.Post, "/api/admin/questions/import/apply",
            new { token = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString() });
        Assert.Equal(HttpStatusCode.OK, appliedFirst.StatusCode);

        // The same file again: the bank already holds it, so the report says so instead of doubling it.
        using var second = await UploadAsync(admin, workbook, bookId, modeId);
        var report = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, report.GetProperty("ready").GetInt32());
        Assert.Equal(1, report.GetProperty("duplicates").GetInt32());
        Assert.Contains("artıq bankdadır", report.GetProperty("rows")[0].GetProperty("problems")[0].GetString());
        Assert.Equal(1, await QuestionCountAsync(factory));
    }

    [Fact]
    public async Task A_file_that_is_not_a_question_bank_is_refused_with_a_reason()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var admin = await SignedInAsync(factory);

        using var notAWorkbook = await UploadAsync(admin, Encoding.UTF8.GetBytes("<?php ?>"), bookId, modeId, "bank.xlsx");
        using var wrongColumns = await UploadAsync(admin, XlsxWriter.Write("Sheet", [new("Ad", 20), new("Soyad", 20)],
            [new object?[] { "Ruslan", "Hüseynov" }]), bookId, modeId);
        using var noBank = await UploadAsync(admin, Workbook([Row("Sual?", "A", "B", "C", "D", "A")]), 9999, modeId);

        Assert.Equal("NOT_A_WORKBOOK", await CodeAsync(notAWorkbook));
        Assert.Equal("COLUMNS_MISSING", await CodeAsync(wrongColumns));
        Assert.Equal("BANK_NOT_FOUND", await CodeAsync(noBank));
        Assert.Equal(0, await QuestionCountAsync(factory));
    }

    [Fact]
    public async Task A_json_file_works_the_same_way_and_the_template_can_be_downloaded()
    {
        await using var factory = NewFactory();
        var (bookId, modeId) = await BankAsync(factory);
        using var admin = await SignedInAsync(factory);
        var json = Encoding.UTF8.GetBytes("""
        [
          { "Sual": "JSON sualı?", "Variant A": "Bir", "Variant B": "İki", "Variant C": "Üç", "Variant D": "Dörd",
            "Düzgün cavab": "C", "Çətinlik": "Çətin", "Alt kateqoriya": "Ədədlər" }
        ]
        """);

        using var upload = await UploadAsync(admin, json, bookId, modeId, "bank.json");
        var report = await upload.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, report.GetProperty("ready").GetInt32());

        using var applied = await SendAsync(admin, HttpMethod.Post, "/api/admin/questions/import/apply",
            new { token = report.GetProperty("token").GetString() });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Questions.AsNoTracking().SingleAsync();
            Assert.Equal(("JSON sualı?", 'C', "Ədədlər"), (stored.Text, stored.CorrectOption, stored.Category));
        }

        // The template is a real workbook with the columns this understands.
        using var template = await admin.GetAsync("/api/admin/questions/import/template.xlsx");
        Assert.Equal(HttpStatusCode.OK, template.StatusCode);
        var rows = XlsxReader.Read(new MemoryStream(await template.Content.ReadAsByteArrayAsync()));
        Assert.Contains("Sual", rows[0]);
        Assert.Contains("Düzgün cavab", rows[0]);
    }

    // --- helpers ---------------------------------------------------------------

    private static object?[] Row(string text, string a, string b, string c, string d, string correct,
        string difficulty = "Orta", string category = "Ümumi") =>
        [text, a, b, c, d, correct, difficulty, category];

    private static byte[] Workbook(IReadOnlyList<object?[]> rows) => XlsxWriter.Write("Suallar",
    [
        new("Sual", 40), new("Variant A", 20), new("Variant B", 20), new("Variant C", 20), new("Variant D", 20),
        new("Düzgün cavab", 12), new("Çətinlik", 10), new("Alt kateqoriya", 16),
    ], rows.Select(r => (IReadOnlyList<object?>)r));

    private static MultipartFormDataContent Multipart(byte[] content, int bookId, int quizModeId, string fileName = "bank.xlsx")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent
        {
            { file, "file", fileName },
            { new StringContent(bookId.ToString()), "bookId" },
            { new StringContent(quizModeId.ToString()), "quizModeId" },
        };
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] content, int bookId, int quizModeId,
        string fileName = "bank.xlsx")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/questions/import")
        {
            Content = Multipart(content, bookId, quizModeId, fileName),
        };
        request.Headers.Add(AdminAuth.CsrfHeader, "1");
        return client.SendAsync(request);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
    }

    /// <summary>A campaign's bank with no questions yet: what an import is for.</summary>
    private static async Task<(int BookId, int QuizModeId)> BankAsync(LeaderboardApiFactory factory)
    {
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campaign = await db.MonthlyCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        return (campaign.BookId!.Value, campaign.QuizModeId);
    }

    private static async Task<int> QuestionCountAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Questions.CountAsync();
    }
}
