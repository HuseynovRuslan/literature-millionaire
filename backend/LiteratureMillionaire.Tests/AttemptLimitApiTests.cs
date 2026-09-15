using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Data;
using LiteratureMillionaire.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>One quiz per phone number per campaign, enforced at start.</summary>
public class AttemptLimitApiTests
{
    [Fact]
    public void Rule_is_one_attempt_per_campaign() => Assert.Equal(1, QuizRules.MaxAttemptsPerCampaign);

    [Fact]
    public async Task An_abandoned_quiz_blocks_a_second_start_with_the_same_phone_in_any_format()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        using var first = await StartAsync(client, "+994507777777");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var start = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, start.GetProperty("attemptNumber").GetInt32());
        Assert.Equal(0, start.GetProperty("remainingAttempts").GetInt32());

        // The first quiz is never answered: starting it already used the attempt.
        using var second = await StartAsync(client, "050 777 77 77");
        await AssertLimitReachedAsync(second);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.Participants.CountAsync());
        Assert.Equal(1, await db.QuizAttempts.CountAsync());
    }

    [Fact]
    public async Task A_completed_quiz_blocks_a_second_start()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        using var first = await StartAsync(client, "+994508888888");
        var start = await first.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();
        for (var questionNumber = 1; questionNumber <= 10; questionNumber++)
        {
            using var answer = await client.PostAsJsonAsync($"/api/game/{sessionId}/answer", new { questionId, selectedOption = "A" });
            var body = await answer.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(HttpStatusCode.OK, answer.StatusCode);
            if (questionNumber < 10)
            {
                questionId = body.GetProperty("nextQuestion").GetProperty("id").GetInt32();
            }
            else
            {
                Assert.True(body.GetProperty("isGameOver").GetBoolean());
            }
        }

        using var second = await StartAsync(client, "994508888888");
        await AssertLimitReachedAsync(second);
    }

    [Fact]
    public async Task The_same_phone_can_take_part_again_in_a_different_campaign()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        var firstCampaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        using (var first = await StartAsync(client, "+994509999999"))
        {
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.MonthlyCampaigns
                .Where(c => c.Id == firstCampaignId)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.IsEnabled, false));
        }

        var secondCampaignId = await factory.SeedPlayableCampaignAsync(includeQuestions: true);
        Assert.NotEqual(firstCampaignId, secondCampaignId);

        using var again = await StartAsync(client, "+994509999999");
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        var body = await again.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("attemptNumber").GetInt32());
    }

    private static Task<HttpResponseMessage> StartAsync(HttpClient client, string phone) =>
        client.PostAsJsonAsync("/api/game/start", new { fullName = "Test İştirakçı", phoneNumber = phone });

    private static async Task AssertLimitReachedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ATTEMPT_LIMIT_REACHED", problem.GetProperty("code").GetString());
        Assert.Equal(1, problem.GetProperty("maxAttempts").GetInt32());
        Assert.Equal(1, problem.GetProperty("attemptsUsed").GetInt32());
    }
}
