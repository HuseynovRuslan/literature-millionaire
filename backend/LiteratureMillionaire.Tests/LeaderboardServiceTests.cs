using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Dtos;
using LiteratureMillionaire.API.Entities;
using LiteratureMillionaire.API.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LiteratureMillionaire.Tests;

public class LeaderboardServiceTests
{
    [Fact]
    public async Task Persistence_query_filters_incomplete_and_other_campaigns_but_keeps_failed_results()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign1, campaign2) = await database.SeedCampaignsAsync();
        var p1 = new Participant { FullName = "Ayşə Məmmədova", NormalizedPhoneNumber = "+994501111111" };
        var p2 = new Participant { FullName = "Ruslan Hüseynov", NormalizedPhoneNumber = "+994502222222" };
        database.Db.Participants.AddRange(p1, p2);
        await database.Db.SaveChangesAsync();

        var completed = new DateTime(2026, 9, 1, 12, 0, 30, DateTimeKind.Utc);
        database.Db.QuizAttempts.AddRange(
            Attempt(p1.Id, campaign1.Id, 1, completed.AddSeconds(-30), completed, 6, 10, passed: false),
            Attempt(p1.Id, campaign1.Id, 2, completed.AddMinutes(1), null, null, null, null),
            Attempt(p2.Id, campaign2.Id, 1, completed.AddSeconds(-10), completed, 10, 20, passed: true));
        await database.Db.SaveChangesAsync();

        var result = await new LeaderboardService(database.Db).GetAsync(campaign1.Id, 10);

        var entry = Assert.Single(result.Entries);
        Assert.Equal("Ayşə M.", entry.DisplayName);
        Assert.Equal(10, entry.PointsEarned);
        Assert.Equal(30, entry.DurationSeconds);

        var inactivePastResult = await new LeaderboardService(database.Db).GetAsync(campaign2.Id, 10);
        Assert.Single(inactivePastResult.Entries);
    }

    [Fact]
    public async Task Position_uses_an_earlier_better_attempt_when_the_latest_attempt_is_weaker()
    {
        await using var database = await TestDatabase.CreateAsync();
        var (campaign, _) = await database.SeedCampaignsAsync();
        var leader = new Participant { FullName = "Leader One", NormalizedPhoneNumber = "+994503333333" };
        var other = new Participant { FullName = "Other Two", NormalizedPhoneNumber = "+994504444444" };
        database.Db.Participants.AddRange(leader, other);
        await database.Db.SaveChangesAsync();
        var completed = new DateTime(2026, 9, 1, 12, 0, 30, DateTimeKind.Utc);

        database.Db.QuizAttempts.AddRange(
            Attempt(leader.Id, campaign.Id, 1, completed.AddSeconds(-30), completed, 10, 20, true),
            Attempt(leader.Id, campaign.Id, 2, completed.AddMinutes(1), completed.AddMinutes(2), 1, 1, false),
            Attempt(other.Id, campaign.Id, 1, completed.AddSeconds(-20), completed, 8, 15, true));
        await database.Db.SaveChangesAsync();

        var service = new LeaderboardService(database.Db);
        Assert.Equal(1, await service.GetPositionAsync(campaign.Id, leader.Id));
        Assert.Equal(2, await service.GetPositionAsync(campaign.Id, other.Id));
    }

    [Fact]
    public void Public_DTOs_do_not_expose_PII_or_internal_identifiers()
    {
        var propertyNames = typeof(LeaderboardDto).GetProperties()
            .Concat(typeof(LeaderboardEntryDto).GetProperties())
            .Select(property => property.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("PhoneNumber", propertyNames);
        Assert.DoesNotContain("NormalizedPhoneNumber", propertyNames);
        Assert.DoesNotContain("ParticipantId", propertyNames);
        Assert.DoesNotContain("QuizAttemptId", propertyNames);
        Assert.DoesNotContain("FullName", propertyNames);
    }

    private static QuizAttempt Attempt(
        int participantId,
        int campaignId,
        int number,
        DateTime started,
        DateTime? completed,
        int? correct,
        int? points,
        bool? passed) => new()
        {
            ParticipantId = participantId,
            CampaignId = campaignId,
            AttemptNumber = number,
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            CorrectAnswers = correct,
            PointsEarned = points,
            Passed = passed,
            TotalQuestions = 10,
            PassingScore = 7,
            MaxPoints = 20
        };

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public ApplicationDbContext Db { get; }

        private TestDatabase(SqliteConnection connection, ApplicationDbContext db)
        {
            _connection = connection;
            Db = db;
        }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            connection.CreateFunction<string, int>("LEN", value => value?.Length ?? 0);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            var db = new ApplicationDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return new TestDatabase(connection, db);
        }

        public async Task<(MonthlyCampaign First, MonthlyCampaign Second)> SeedCampaignsAsync()
        {
            var mode = new QuizMode { Slug = QuizModeSlugs.BilikDunyasi, Title = "Bilik Dünyası", Description = "Test", IconKey = "globe", DisplayOrder = 1 };
            var firstBook = Book("First");
            var secondBook = Book("Second");
            var first = Campaign(firstBook, mode);
            var second = Campaign(secondBook, mode);
            second.StartDate = new DateOnly(2025, 1, 1);
            second.EndDate = new DateOnly(2025, 1, 31);
            second.IsEnabled = false;
            Db.AddRange(first, second);
            await Db.SaveChangesAsync();
            return (first, second);
        }

        private static Book Book(string title) => new()
        {
            Title = title,
            Author = "Author",
            Description = "Description",
            CoverImageUrl = "/cover.jpg",
            IsActive = true
        };

        private static MonthlyCampaign Campaign(Book book, QuizMode mode) => new()
        {
            Book = book,
            QuizMode = mode,
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            PassingScore = 7,
            RewardTitle = "Reward",
            IsEnabled = true
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
