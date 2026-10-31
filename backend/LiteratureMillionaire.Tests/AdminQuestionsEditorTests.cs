using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// The question editor (phase 6). A question typed in the panel is played next to the generated banks, so it is
/// held to the same rules they were: four options that are really different, alt text that does not give the
/// answer away, and a credit that stays with a picture.
/// </summary>
public class AdminQuestionsEditorTests
{
    [Fact]
    public async Task The_editor_needs_an_admin_session_and_changes_need_the_admin_header()
    {
        await using var factory = NewFactory();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using var anonymous = Https(factory);

        using var list = await anonymous.GetAsync("/api/admin/questions");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsJsonAsync("/api/admin/questions", new { });
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal("ADMIN_HEADER_REQUIRED", (await headerless.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Questions_can_be_found_by_bank_difficulty_picture_and_words()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (bookId, modeId) = await BankAsync(factory, campaignId);
        using var admin = await SignedInAsync(factory);
        await CreateAsync(admin, Input(bookId, modeId, text: "Bakının ən qədim qapısı hansıdır?", category: "Şəhər"));

        var all = await admin.GetFromJsonAsync<JsonElement>("/api/admin/questions");
        Assert.Equal(11, all.GetProperty("total").GetInt32()); // ten seeded plus the one just written

        var easy = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/questions?bookId={bookId}&difficulty=Easy");
        Assert.Equal(3, easy.GetProperty("total").GetInt32());

        var found = await admin.GetFromJsonAsync<JsonElement>("/api/admin/questions?search=qapısı");
        var row = Assert.Single(found.GetProperty("questions").EnumerateArray());
        Assert.Equal("Bakının ən qədim qapısı hansıdır?", row.GetProperty("text").GetString());
        Assert.Equal("Şəhər", row.GetProperty("category").GetString());

        var withImage = await admin.GetFromJsonAsync<JsonElement>("/api/admin/questions?withImage=true");
        Assert.Equal(0, withImage.GetProperty("total").GetInt32());

        // A page is a page: the count is what the filter matched, not what came back.
        var page = await admin.GetFromJsonAsync<JsonElement>("/api/admin/questions?take=4");
        Assert.Equal(11, page.GetProperty("total").GetInt32());
        Assert.Equal(4, page.GetProperty("questions").GetArrayLength());

        var options = await admin.GetFromJsonAsync<JsonElement>("/api/admin/questions/options");
        var bank = options.GetProperty("banks").EnumerateArray().Single(b => b.GetProperty("id").GetInt32() == bookId);
        // Four seeded medium questions, plus the one written above.
        Assert.Equal(5, bank.GetProperty("medium").GetInt32());
        Assert.Contains(options.GetProperty("categories").EnumerateArray(), c => c.GetString() == "Şəhər");
    }

    [Fact]
    public async Task A_written_question_is_playable_and_recorded()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (bookId, modeId) = await BankAsync(factory, campaignId);
        using var admin = await SignedInAsync(factory);

        using var created = await CreateAsync(admin, Input(bookId, modeId, text: "Hansı ağac payızda qırmızı olur?"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetInt32();
        Assert.Equal("Test Book", body.GetProperty("bookTitle").GetString());
        Assert.Equal("B", body.GetProperty("correctOption").GetString());

        // It belongs to the bank the game draws from: mode and book are what the planner filters on.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Questions.AsNoTracking().SingleAsync(q => q.Id == id);
            Assert.Equal((bookId, modeId), (stored.BookId, stored.QuizModeId));
        }

        var entry = await LastAuditAsync(factory);
        Assert.Equal("question-created", entry.Action);
        Assert.Equal(id.ToString(), entry.EntityId);

        using var edited = await SendAsync(admin, HttpMethod.Put, $"/api/admin/questions/{id}",
            Input(bookId, modeId, text: "Hansı ağac payızda qırmızı olur?", optionB: "Ağcaqayın (acer)", correct: "B"));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Contains("Variant B", (await LastAuditAsync(factory)).Details);

        using var deleted = await SendAsync(admin, HttpMethod.Delete, $"/api/admin/questions/{id}", null);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var trail = await LastAuditAsync(factory);
        Assert.Equal("question-deleted", trail.Action);
        // What it said is kept: a deletion has to be explainable after the row is gone.
        Assert.Contains("Hansı ağac payızda qırmızı olur?", trail.Details);

        using var again = await SendAsync(admin, HttpMethod.Delete, $"/api/admin/questions/{id}", null);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task A_question_nobody_could_answer_is_refused()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (bookId, modeId) = await BankAsync(factory, campaignId);
        using var admin = await SignedInAsync(factory);

        // Two options that are the same answer: one of the buttons cannot be right.
        using var repeated = await CreateAsync(admin, Input(bookId, modeId, optionC: "palıd", optionA: "Palıd"));
        Assert.Contains("fərqli", (await Errors(repeated)).GetProperty("optionC")[0].GetString());

        using var blank = await CreateAsync(admin, Input(bookId, modeId, text: "   "));
        Assert.True((await Errors(blank)).TryGetProperty("text", out _));

        using var noBank = await CreateAsync(admin, Input(bookId: 9999, modeId, text: "Sual?"));
        Assert.True((await Errors(noBank)).TryGetProperty("bookId", out _));

        // A question with no category is never drawn by the game, so it is not something to save "for now".
        using var noMode = await CreateAsync(admin, Input(bookId, quizModeId: null, text: "Sual?"));
        Assert.True((await Errors(noMode)).TryGetProperty("quizModeId", out _));

        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Equal(10, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Questions.CountAsync());
    }

    [Fact]
    public async Task An_illustrated_question_keeps_its_credit_and_never_gives_the_answer_away()
    {
        await using var factory = NewFactory();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var (bookId, modeId) = await BankAsync(factory, campaignId);
        using var admin = await SignedInAsync(factory);
        var picture = "/uploads/questions/" + new string('a', 32) + ".webp";

        // Alt text is read out before the options: naming the answer in it hands the question away.
        using var tellsAnswer = await CreateAsync(admin, Input(bookId, modeId, image: picture,
            alt: "Şəkildə ağcaqayın yarpağı görünür", source: "Öz arxivimiz", licence: "Şirkətin öz şəkli",
            optionB: "Ağcaqayın", correct: "B"));
        Assert.Contains("düzgün cavabı deməməlidir", (await Errors(tellsAnswer)).GetProperty("imageAltText")[0].GetString());

        // A picture with no credit: some of them are only ours to show on that condition.
        using var noCredit = await CreateAsync(admin, Input(bookId, modeId, image: picture, alt: "Payız yarpağı"));
        var errors = await Errors(noCredit);
        Assert.True(errors.TryGetProperty("imageSource", out _));
        Assert.True(errors.TryGetProperty("imageLicense", out _));

        // Somewhere else entirely.
        using var foreign = await CreateAsync(admin, Input(bookId, modeId, image: "https://example.com/leaf.webp",
            alt: "Payız yarpağı", source: "Öz arxivimiz", licence: "Şirkətin öz şəkli"));
        Assert.True((await Errors(foreign)).TryGetProperty("imageUrl", out _));

        // Credits left behind after the picture was removed would credit nothing.
        using var orphaned = await CreateAsync(admin, Input(bookId, modeId, alt: "Payız yarpağı", source: "Öz arxivimiz"));
        Assert.True((await Errors(orphaned)).TryGetProperty("imageUrl", out _));

        using var good = await CreateAsync(admin, Input(bookId, modeId, image: picture, alt: "Payız rəngli yarpaq",
            source: "Öz arxivimiz", licence: "Şirkətin öz şəkli"));
        Assert.Equal(HttpStatusCode.Created, good.StatusCode);
        var stored = await good.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(picture, stored.GetProperty("imageUrl").GetString());
        Assert.Equal("Şirkətin öz şəkli", stored.GetProperty("imageLicense").GetString());
    }

    // --- helpers ---------------------------------------------------------------

    private static object Input(int bookId, int? quizModeId, string text = "Hansı ağac payızda qırmızı olur?",
        string optionA = "Palıd", string optionB = "Ağcaqayın", string optionC = "Şam", string optionD = "Sərv",
        string correct = "B", string difficulty = "Medium", string category = "Bitkilər", string? image = null,
        string? alt = null, string? source = null, string? licence = null) => new
    {
        text,
        optionA,
        optionB,
        optionC,
        optionD,
        correctOption = correct,
        difficulty,
        category,
        explanation = (string?)null,
        bookId,
        quizModeId,
        imageUrl = image,
        imageAltText = alt,
        imageSource = source,
        imageLicense = licence,
    };

    private static Task<HttpResponseMessage> CreateAsync(HttpClient admin, object input) =>
        SendAsync(admin, HttpMethod.Post, "/api/admin/questions", input);

    private static async Task<JsonElement> Errors(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QUESTION_INVALID", body.GetProperty("code").GetString());
        return body.GetProperty("errors");
    }

    /// <summary>The seeded campaign's bank: the book its questions belong to, and the mode they are played in.</summary>
    private static async Task<(int BookId, int QuizModeId)> BankAsync(LeaderboardApiFactory factory, int campaignId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campaign = await db.MonthlyCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        return (campaign.BookId!.Value, campaign.QuizModeId);
    }

    private static async Task<AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }
}
