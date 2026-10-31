using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static LiteratureMillionaire.Tests.AdminTestClient;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// Results and participants in the admin panel (phase 3): the ranking prizes are given by, the Excel list, and the
/// corrections that delete data - each with a reason, recorded, and never while someone may still be playing.
/// </summary>
public class AdminResultsTests
{
    [Fact]
    public async Task Results_and_the_export_need_an_admin_session_and_corrections_need_the_admin_header()
    {
        await using var factory = NewFactory();
        var seeded = await SeedAsync(factory);
        using var anonymous = Https(factory);

        using var results = await anonymous.GetAsync($"/api/admin/campaigns/{seeded.CampaignId}/results");
        using var export = await anonymous.GetAsync($"/api/admin/campaigns/{seeded.CampaignId}/results.xlsx");
        Assert.Equal(HttpStatusCode.Unauthorized, results.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, export.StatusCode);

        using var admin = await SignedInAsync(factory);
        using var headerless = await admin.PostAsJsonAsync($"/api/admin/attempts/{seeded.Winner}/reset", new { reason = "test girişi" });
        Assert.Equal(HttpStatusCode.BadRequest, headerless.StatusCode);
        Assert.Equal(3, await AttemptCountAsync(factory));
    }

    [Fact]
    public async Task Results_are_ranked_like_the_leaderboard_with_phones_masked_and_can_be_searched()
    {
        await using var factory = NewFactory();
        var seeded = await SeedAsync(factory);
        using var admin = await SignedInAsync(factory);

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/campaigns/{seeded.CampaignId}/results");

        Assert.Equal(3, body.GetProperty("started").GetInt32());
        Assert.Equal(2, body.GetProperty("completed").GetInt32());
        Assert.Equal(1, body.GetProperty("passed").GetInt32());
        Assert.Equal(1, body.GetProperty("unfinished").GetInt32());

        var rows = body.GetProperty("attempts").EnumerateArray().ToList();
        Assert.Equal(["Şükür Əliyev", "Test İkinci", "Test Bitməyən"], rows.Select(r => r.GetProperty("fullName").GetString()));
        Assert.Equal(1, rows[0].GetProperty("rank").GetInt32());
        Assert.Equal(2, rows[1].GetProperty("rank").GetInt32());
        Assert.Equal(JsonValueKind.Null, rows[2].GetProperty("rank").ValueKind);
        Assert.Equal("+994 55 *** ** 01", rows[0].GetProperty("phone").GetString());
        Assert.DoesNotContain("0000101", body.GetRawText());
        // The winner also played another campaign: removing them would delete both.
        Assert.Equal(2, rows[0].GetProperty("participantAttempts").GetInt32());

        // Typed without Azerbaijani letters, or by part of the number.
        var byName = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/campaigns/{seeded.CampaignId}/results?search=sukur");
        var byPhone = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/campaigns/{seeded.CampaignId}/results?search=0102");
        Assert.Equal("Şükür Əliyev", Assert.Single(byName.GetProperty("attempts").EnumerateArray()).GetProperty("fullName").GetString());
        Assert.Equal("Test İkinci", Assert.Single(byPhone.GetProperty("attempts").EnumerateArray()).GetProperty("fullName").GetString());
        Assert.Equal(3, byPhone.GetProperty("started").GetInt32()); // the totals stay the campaign's

        using var missing = await admin.GetAsync("/api/admin/campaigns/9999/results");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task The_excel_export_holds_full_numbers_is_never_cached_and_is_recorded()
    {
        await using var factory = NewFactory();
        var seeded = await SeedAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.QuizAttempts.Add(new QuizAttempt
            {
                Participant = new Participant { FullName = "=HYPERLINK(\"x\")", NormalizedPhoneNumber = "+994550000104" },
                CampaignId = seeded.CampaignId, AttemptNumber = 1, StartedAtUtc = DateTime.UtcNow.AddHours(-5),
                CompletedAtUtc = DateTime.UtcNow.AddHours(-5).AddSeconds(80), CorrectAnswers = 2, PointsEarned = 3, Passed = false,
                TotalQuestions = 10, PassingScore = 7, MaxPoints = 20,
            });
            await db.SaveChangesAsync();
        }
        using var admin = await SignedInAsync(factory);

        using var export = await admin.GetAsync($"/api/admin/campaigns/{seeded.CampaignId}/results.xlsx");

        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", export.Content.Headers.ContentType?.MediaType);
        Assert.Contains("no-store", export.Headers.CacheControl?.ToString());
        Assert.EndsWith(".xlsx", export.Content.Headers.ContentDisposition?.FileNameStar ?? export.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

        using var zip = new ZipArchive(await export.Content.ReadAsStreamAsync());
        Assert.NotNull(zip.GetEntry("xl/workbook.xml"));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = await reader.ReadToEndAsync();
        Assert.Contains("Ad Soyad", sheet);
        Assert.Contains("+994550000101", sheet);
        Assert.Contains("Şükür Əliyev", sheet);
        Assert.Contains("Bitməyib", sheet);
        // Text is text: a name that looks like a formula is escaped, never written as one.
        Assert.Contains("=HYPERLINK(&quot;x&quot;)", sheet);
        Assert.DoesNotContain("<f>", sheet);

        var entry = await LastAuditAsync(factory);
        Assert.Equal("results-exported", entry.Action);
        Assert.Equal(seeded.CampaignId.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Resetting_an_attempt_needs_a_reason_waits_for_a_game_in_play_and_is_recorded()
    {
        await using var factory = NewFactory();
        var seeded = await SeedAsync(factory);
        using var admin = await SignedInAsync(factory);

        using var noReason = await SendAsync(admin, HttpMethod.Post, $"/api/admin/attempts/{seeded.Winner}/reset", new { reason = " " });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("REASON_REQUIRED", await CodeAsync(noReason));

        // Started a minute ago and not finished: the player may be on question five right now.
        using var inPlay = await SendAsync(admin, HttpMethod.Post, $"/api/admin/attempts/{seeded.Playing}/reset", new { reason = "yenidən oynasın" });
        Assert.Equal(HttpStatusCode.Conflict, inPlay.StatusCode);
        Assert.Equal("ATTEMPT_IN_PLAY", await CodeAsync(inPlay));

        using var reset = await SendAsync(admin, HttpMethod.Post, $"/api/admin/attempts/{seeded.Winner}/reset", new { reason = "texniki nasazlıq" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var body = await admin.GetFromJsonAsync<JsonElement>($"/api/admin/campaigns/{seeded.CampaignId}/results");
        Assert.DoesNotContain(body.GetProperty("attempts").EnumerateArray(), r => r.GetProperty("attemptId").GetInt32() == seeded.Winner);
        Assert.Equal(1, body.GetProperty("attempts")[0].GetProperty("rank").GetInt32()); // the runner-up moves up

        var entry = await LastAuditAsync(factory);
        Assert.Equal("attempt-reset", entry.Action);
        Assert.Contains("Şükür Əliyev", entry.Details);
        Assert.Contains("9/10 düzgün", entry.Details);
        Assert.Contains("səbəb: texniki nasazlıq", entry.Details);

        using var again = await SendAsync(admin, HttpMethod.Post, $"/api/admin/attempts/{seeded.Winner}/reset", new { reason = "texniki nasazlıq" });
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task Removing_a_participant_deletes_them_everywhere_and_nobody_else()
    {
        await using var factory = NewFactory();
        var seeded = await SeedAsync(factory);
        using var admin = await SignedInAsync(factory);

        using var removed = await SendAsync(admin, HttpMethod.Post, $"/api/admin/participants/{seeded.WinnerParticipant}/remove", new { reason = "test girişi" });
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.Participants.AnyAsync(p => p.Id == seeded.WinnerParticipant));
            Assert.False(await db.QuizAttempts.AnyAsync(a => a.ParticipantId == seeded.WinnerParticipant));
            Assert.Equal(2, await db.Participants.CountAsync());
            Assert.Equal(2, await db.QuizAttempts.CountAsync());
        }

        var entry = await LastAuditAsync(factory);
        Assert.Equal("participant-removed", entry.Action);
        Assert.Contains("2 cəhd", entry.Details);
        Assert.Contains("səbəb: test girişi", entry.Details);

        // Someone mid-game cannot be removed.
        using var playing = await SendAsync(admin, HttpMethod.Post, $"/api/admin/participants/{seeded.PlayingParticipant}/remove", new { reason = "test girişi" });
        Assert.Equal(HttpStatusCode.Conflict, playing.StatusCode);
    }

    // --- helpers ---------------------------------------------------------------

    private sealed record Seeded(int CampaignId, int Winner, int WinnerParticipant, int Playing, int PlayingParticipant);

    /// <summary>
    /// A campaign with a winner (9/10), a runner-up (6/10) and a game started a minute ago,
    /// plus the winner's attempt in a second campaign.
    /// </summary>
    private static async Task<Seeded> SeedAsync(LeaderboardApiFactory factory)
    {
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: false);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var campaign = await db.MonthlyCampaigns.AsNoTracking().SingleAsync(c => c.Id == campaignId);
        var other = new MonthlyCampaign
        {
            QuizModeId = campaign.QuizModeId, BookId = campaign.BookId, StartDate = campaign.StartDate.AddMonths(-2),
            EndDate = campaign.StartDate.AddMonths(-1), PassingScore = 7, RewardTitle = "Keçən ay", IsEnabled = false,
        };
        db.MonthlyCampaigns.Add(other);

        var now = DateTime.UtcNow;
        var winner = new Participant { FullName = "Şükür Əliyev", NormalizedPhoneNumber = "+994550000101" };
        var second = new Participant { FullName = "Test İkinci", NormalizedPhoneNumber = "+994550000102" };
        var playing = new Participant { FullName = "Test Bitməyən", NormalizedPhoneNumber = "+994550000103" };

        QuizAttempt Attempt(Participant p, MonthlyCampaign? c, DateTime started, int? correct, int? points, bool? passed) => new()
        {
            Participant = p, CampaignId = c?.Id ?? campaignId, Campaign = c!, AttemptNumber = 1, StartedAtUtc = started,
            CompletedAtUtc = correct is null ? null : started.AddSeconds(90), CorrectAnswers = correct, PointsEarned = points,
            Passed = passed, TotalQuestions = 10, PassingScore = 7, MaxPoints = 20,
        };

        var won = Attempt(winner, null, now.AddHours(-3), 9, 18, true);
        var runnerUp = Attempt(second, null, now.AddHours(-2), 6, 11, false);
        var inPlay = Attempt(playing, null, now.AddMinutes(-1), null, null, null);
        db.QuizAttempts.AddRange(won, runnerUp, inPlay, Attempt(winner, other, now.AddDays(-40), 5, 9, false));
        await db.SaveChangesAsync();

        return new Seeded(campaignId, won.Id, winner.Id, inPlay.Id, playing.Id);
    }

    private static async Task<int> AttemptCountAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().QuizAttempts.CountAsync(a => a.Campaign.RewardTitle != "Keçən ay");
    }

    private static async Task<AdminAuditEntry> LastAuditAsync(LeaderboardApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AdminAuditEntries.AsNoTracking().OrderByDescending(e => e.Id).FirstAsync();
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
