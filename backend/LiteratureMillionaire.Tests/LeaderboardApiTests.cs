using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Seed;
using LiteratureMillionaire.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace LiteratureMillionaire.Tests;

public class LeaderboardApiTests
{
    [Fact]
    public async Task Missing_campaign_returns_404_with_stable_code()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.CreateDatabaseAsync();

        using var response = await client.GetAsync("/api/campaigns/404/leaderboard");
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("CAMPAIGN_NOT_FOUND", problem.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Empty_campaign_returns_200_and_empty_entries()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: false);

        using var response = await client.GetAsync($"/api/campaigns/{campaignId}/leaderboard");
        var leaderboard = await response.Content.ReadFromJsonAsync<LeaderboardDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(leaderboard);
        Assert.Equal(campaignId, leaderboard.CampaignId);
        Assert.Empty(leaderboard.Entries);
        Assert.Equal(DateTimeKind.Utc, leaderboard.GeneratedAtUtc.Kind);
    }

    [Fact]
    public async Task Leaderboard_carries_its_campaigns_quiz_mode()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var bilikId = await factory.SeedPlayableCampaignAsync(includeQuestions: false, modeSlug: QuizModeSlugs.BilikDunyasi);
        var ayinId = await factory.SeedPlayableCampaignAsync(includeQuestions: false, modeSlug: QuizModeSlugs.AyinKitabi);

        using var bilikResponse = await client.GetAsync($"/api/campaigns/{bilikId}/leaderboard");
        var bilikBoard = await bilikResponse.Content.ReadFromJsonAsync<LeaderboardDto>();
        using var ayinResponse = await client.GetAsync($"/api/campaigns/{ayinId}/leaderboard");
        var ayinBoard = await ayinResponse.Content.ReadFromJsonAsync<LeaderboardDto>();

        Assert.NotNull(bilikBoard);
        Assert.Equal(QuizModeSlugs.BilikDunyasi, bilikBoard.QuizMode.Slug);
        Assert.Equal("Bilik Dünyası", bilikBoard.QuizMode.Title);
        Assert.NotNull(ayinBoard);
        Assert.Equal(QuizModeSlugs.AyinKitabi, ayinBoard.QuizMode.Slug);
        Assert.Equal("Ayın Kitabı", ayinBoard.QuizMode.Title);
    }

    [Theory]
    [InlineData(1, HttpStatusCode.OK)]
    [InlineData(10, HttpStatusCode.OK)]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(11, HttpStatusCode.BadRequest)]
    public async Task Limit_boundaries_use_standard_api_validation(int limit, HttpStatusCode expected)
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: false);

        using var response = await client.GetAsync($"/api/campaigns/{campaignId}/leaderboard?limit={limit}");

        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.True(problem.TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public async Task Only_final_answer_contains_campaign_and_best_attempt_position()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var campaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        var start = await StartAsync(client, "+994505555555");
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();

        for (var questionNumber = 1; questionNumber <= 10; questionNumber++)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/game/{start.GetProperty("sessionId").GetGuid()}/answer",
                new { questionId, selectedOption = "A" });
            var jsonText = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonText);
            var body = document.RootElement;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (questionNumber < 10)
            {
                Assert.Equal(JsonValueKind.Null, body.GetProperty("result").ValueKind);
                Assert.DoesNotContain("leaderboardPosition", jsonText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("campaignId", jsonText, StringComparison.OrdinalIgnoreCase);
                questionId = body.GetProperty("nextQuestion").GetProperty("id").GetInt32();
            }
            else
            {
                var result = body.GetProperty("result");
                Assert.True(body.GetProperty("isGameOver").GetBoolean());
                Assert.Equal(campaignId, result.GetProperty("campaignId").GetInt32());
                Assert.Equal(1, result.GetProperty("leaderboardPosition").GetInt32());
            }
        }
    }

    [Fact]
    public async Task Position_failure_does_not_fail_the_persisted_final_result()
    {
        await using var factory = new LeaderboardApiFactory(failPositionLookup: true);
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        var start = await StartAsync(client, "+994506666666");
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();

        JsonElement final = default;
        for (var questionNumber = 1; questionNumber <= 10; questionNumber++)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/game/{sessionId}/answer",
                new { questionId, selectedOption = "A" });
            final = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (questionNumber < 10)
            {
                questionId = final.GetProperty("nextQuestion").GetProperty("id").GetInt32();
            }
        }

        Assert.Equal(JsonValueKind.Null, final.GetProperty("result").GetProperty("leaderboardPosition").ValueKind);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var attempt = await db.QuizAttempts.AsNoTracking().SingleAsync();
        Assert.NotNull(attempt.CompletedAtUtc);
        Assert.NotNull(attempt.PointsEarned);
    }

    private static async Task<JsonElement> StartAsync(HttpClient client, string phone)
    {
        using var response = await client.PostAsJsonAsync("/api/game/start", new
        {
            fullName = "Ayşə Məmmədova",
            phoneNumber = phone
        });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return body;
    }
}

internal sealed class LeaderboardApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly bool _failPositionLookup;
    private readonly string? _qrLogSecret;
    private readonly bool? _requireQrLogin;
    private readonly string? _adminPhones;
    private readonly string? _uploadsRoot;

    /// <summary>Every log line written by the API during the test (for PII checks).</summary>
    public ConcurrentQueue<string> Logs { get; } = new();

    /// <param name="qrLogSecret">The secret QRLog signs sign-in confirmations with; null leaves it unconfigured.</param>
    /// <param name="requireQrLogin">Game:RequireQrLogin; null keeps the Testing default (not required).</param>
    /// <param name="adminPhones">Admin:Phones; null leaves the admin list empty (nobody is an admin).</param>
    /// <param name="uploadsRoot">Uploads:Root; null leaves the default (a folder under the content root).</param>
    public LeaderboardApiFactory(bool failPositionLookup = false, string? qrLogSecret = null, bool? requireQrLogin = null,
        string? adminPhones = null, string? uploadsRoot = null)
    {
        _adminPhones = adminPhones;
        _uploadsRoot = uploadsRoot;
        _failPositionLookup = failPositionLookup;
        _qrLogSecret = qrLogSecret;
        _requireQrLogin = requireQrLogin;
        _connection.Open();
        _connection.CreateFunction<string, int>("LEN", value => value?.Length ?? 0);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)));
        var settings = new Dictionary<string, string?>();
        if (_qrLogSecret is not null) settings["QrLog:VouchSecret"] = _qrLogSecret;
        if (_requireQrLogin is not null) settings["Game:RequireQrLogin"] = _requireQrLogin.Value ? "true" : "false";
        if (_adminPhones is not null) settings["Admin:Phones"] = _adminPhones;
        if (_uploadsRoot is not null) settings["Uploads:Root"] = _uploadsRoot;
        if (settings.Count > 0)
        {
            builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));
        }
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ApplicationDbContext>();
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));

            if (_failPositionLookup)
            {
                services.RemoveAll<ILeaderboardService>();
                services.AddScoped<ILeaderboardService, FailingPositionLeaderboardService>();
            }
        });
    }

    public async Task CreateDatabaseAsync()
    {
        _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task<int> SeedPlayableCampaignAsync(bool includeQuestions, string modeSlug = QuizModeSlugs.BilikDunyasi)
    {
        await CreateDatabaseAsync();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await QuizModeSeed.SeedAsync(db);
        var quizModeId = await QuizModeSeed.GetIdAsync(db, modeSlug);
        var book = new Book
        {
            Title = "Test Book",
            Author = "Test Author",
            Description = "Test Description",
            CoverImageUrl = "/test.jpg",
            IsActive = true
        };
        var campaign = new MonthlyCampaign
        {
            Book = book,
            QuizModeId = quizModeId,
            StartDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)),
            EndDate = DateOnly.FromDateTime(DateTime.Now.AddDays(1)),
            PassingScore = 7,
            RewardTitle = "Test Reward",
            IsEnabled = true
        };
        db.Add(campaign);

        if (includeQuestions)
        {
            var difficulties = Enumerable.Repeat(Difficulty.Easy, 3)
                .Concat(Enumerable.Repeat(Difficulty.Medium, 4))
                .Concat(Enumerable.Repeat(Difficulty.Hard, 3));
            var number = 0;
            foreach (var difficulty in difficulties)
            {
                number++;
                db.Questions.Add(new Question
                {
                    Book = book,
                    QuizModeId = quizModeId,
                    Text = $"Question {number}",
                    OptionA = "A",
                    OptionB = "B",
                    OptionC = "C",
                    OptionD = "D",
                    CorrectOption = 'A',
                    Difficulty = difficulty,
                    Category = "Test"
                });
            }
        }

        await db.SaveChangesAsync();
        return campaign.Id;
    }

    public new async ValueTask DisposeAsync()
    {
        Dispose();
        await _connection.DisposeAsync();
    }

    private sealed class FailingPositionLeaderboardService : ILeaderboardService
    {
        public Task<LeaderboardDto> GetAsync(int campaignId, int limit, CancellationToken ct = default) =>
            throw new InvalidOperationException("test failure");

        public Task<int?> GetPositionAsync(int campaignId, int participantId, CancellationToken ct = default) =>
            throw new InvalidOperationException("test failure");
    }
}

/// <summary>Collects every log line (message and exception text) written during a test, for PII checks.</summary>
internal sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(sink, categoryName);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> sink, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            sink.Enqueue($"{logLevel} {category}: {formatter(state, exception)} {exception}");
    }
}
