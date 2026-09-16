using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LiteratureMillionaire.API.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace LiteratureMillionaire.Tests;

/// <summary>
/// The end-of-quiz answer review: every question with its correct answer, disclosed on the result
/// screen and nowhere else. The in-game silence rule is the point of these tests - a round that leaked
/// a single correct answer early would be worse than no review at all.
/// </summary>
public class AnswerReviewApiTests
{
    private const string FullName = "Ayşə Məmmədova";

    /// <summary>Anything that would give an answer away while the quiz is still running.</summary>
    private static readonly string[] Disclosures =
        ["review", "correctOption", "correctAnswer", "isCorrect", "explanation", "selectedAnswer", "pointsEarned"];

    [Fact]
    public async Task Review_is_withheld_for_every_question_and_arrives_only_with_the_final_result()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        var start = await StartAsync(client, "+994507000001");
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();

        // The first question is delivered by /start, which must be as silent as the answers that follow.
        var startJson = start.GetRawText();
        Assert.All(Disclosures, key => Assert.DoesNotContain(key, startJson, StringComparison.OrdinalIgnoreCase));

        JsonElement result = default;
        for (var number = 1; number <= 10; number++)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/game/{sessionId}/answer", new { questionId, selectedOption = "A" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(json);

            if (number < 10)
            {
                Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("result").ValueKind);
                Assert.All(Disclosures, key => Assert.DoesNotContain(key, json, StringComparison.OrdinalIgnoreCase));
                questionId = document.RootElement.GetProperty("nextQuestion").GetProperty("id").GetInt32();
            }
            else
            {
                result = document.RootElement.GetProperty("result").Clone();
            }
        }

        var review = result.GetProperty("review").EnumerateArray().ToList();
        Assert.Equal(10, review.Count);
        Assert.Equal(Enumerable.Range(1, 10), review.Select(item => item.GetProperty("questionNumber").GetInt32()));

        foreach (var item in review)
        {
            // Every seeded question stores A as the right option, whatever letter this session showed it under.
            Assert.Equal("A", item.GetProperty("correctAnswer").GetString());
            Assert.Contains(item.GetProperty("correctOption").GetString(), new[] { "A", "B", "C", "D" });
            Assert.Equal("A", item.GetProperty("selectedOption").GetString());
            Assert.Equal(item.GetProperty("correctOption").GetString() == "A", item.GetProperty("isCorrect").GetBoolean());
            Assert.False(item.GetProperty("timedOut").GetBoolean());
            Assert.StartsWith("Question ", item.GetProperty("text").GetString());
        }

        // The review and the score are two views of the same server-side truth; they cannot disagree.
        Assert.Equal(
            result.GetProperty("correctAnswers").GetInt32(),
            review.Count(item => item.GetProperty("isCorrect").GetBoolean()));
        Assert.Equal(
            result.GetProperty("pointsEarned").GetInt32(),
            review.Where(item => item.GetProperty("isCorrect").GetBoolean()).Sum(item => item.GetProperty("points").GetInt32()));
        Assert.Equal(result.GetProperty("maxPoints").GetInt32(), review.Sum(item => item.GetProperty("points").GetInt32()));

        // Points follow the difficulty ladder, so the review explains the score rather than restating it.
        Assert.All(review, item => Assert.Equal(
            item.GetProperty("difficulty").GetString() switch { "Easy" => 1, "Medium" => 2, _ => 3 },
            item.GetProperty("points").GetInt32()));
    }

    [Fact]
    public async Task A_question_closed_by_the_clock_is_reviewed_as_unanswered()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        var start = await StartAsync(client, "+994507000002");
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();

        ExpireCurrentQuestion(factory, sessionId);
        using var timedOut = await client.PostAsJsonAsync($"/api/game/{sessionId}/timeout", new { questionId });
        Assert.Equal(HttpStatusCode.OK, timedOut.StatusCode);
        var afterTimeout = await timedOut.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterTimeout.GetProperty("timedOut").GetBoolean());
        questionId = afterTimeout.GetProperty("nextQuestion").GetProperty("id").GetInt32();

        var result = await FinishAsync(client, sessionId, questionId, fromQuestion: 2);
        var first = result.GetProperty("review").EnumerateArray().First();
        Assert.Equal(1, first.GetProperty("questionNumber").GetInt32());
        Assert.Equal(JsonValueKind.Null, first.GetProperty("selectedOption").ValueKind);
        Assert.Equal(JsonValueKind.Null, first.GetProperty("selectedAnswer").ValueKind);
        Assert.True(first.GetProperty("timedOut").GetBoolean());
        Assert.False(first.GetProperty("isCorrect").GetBoolean());
        // Unanswered still shows the right answer: that is what the player came to the screen to see.
        Assert.Equal("A", first.GetProperty("correctAnswer").GetString());
    }

    [Fact]
    public async Task A_late_answer_is_reviewed_as_the_choice_the_player_made_but_never_as_correct()
    {
        await using var factory = new LeaderboardApiFactory();
        using var client = factory.CreateClient();
        await factory.SeedPlayableCampaignAsync(includeQuestions: true);

        var start = await StartAsync(client, "+994507000003");
        var sessionId = start.GetProperty("sessionId").GetGuid();
        var questionId = start.GetProperty("question").GetProperty("id").GetInt32();

        // Find the letter that would have been right, then submit exactly that - too late.
        var correctLetter = CurrentCorrectLetter(factory, sessionId);
        ExpireCurrentQuestion(factory, sessionId);
        using var late = await client.PostAsJsonAsync(
            $"/api/game/{sessionId}/answer", new { questionId, selectedOption = correctLetter.ToString() });
        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        var afterLate = await late.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(afterLate.GetProperty("timedOut").GetBoolean());
        questionId = afterLate.GetProperty("nextQuestion").GetProperty("id").GetInt32();

        var result = await FinishAsync(client, sessionId, questionId, fromQuestion: 2);
        var first = result.GetProperty("review").EnumerateArray().First();
        Assert.Equal(correctLetter.ToString(), first.GetProperty("selectedOption").GetString());
        Assert.Equal(correctLetter.ToString(), first.GetProperty("correctOption").GetString());
        Assert.True(first.GetProperty("timedOut").GetBoolean());
        Assert.False(first.GetProperty("isCorrect").GetBoolean());
    }

    // --- helpers ---------------------------------------------------------------

    private static GameSession Session(LeaderboardApiFactory factory, Guid sessionId) =>
        factory.Services.GetRequiredService<IMemoryCache>().Get<GameSession>($"game-session:{sessionId}")
        ?? throw new InvalidOperationException($"Session {sessionId} is not cached.");

    /// <summary>Moves the current deadline into the past, so the clock can be tested without waiting for it.</summary>
    private static void ExpireCurrentQuestion(LeaderboardApiFactory factory, Guid sessionId) =>
        Session(factory, sessionId).QuestionDeadlineUtc = DateTime.UtcNow.AddSeconds(-1);

    private static char CurrentCorrectLetter(LeaderboardApiFactory factory, Guid sessionId) =>
        Session(factory, sessionId).Current.CorrectDisplayOption;

    /// <summary>Answers A from <paramref name="fromQuestion"/> to the end and returns the final result.</summary>
    private static async Task<JsonElement> FinishAsync(HttpClient client, Guid sessionId, int questionId, int fromQuestion)
    {
        JsonElement body = default;
        for (var number = fromQuestion; number <= 10; number++)
        {
            using var response = await client.PostAsJsonAsync(
                $"/api/game/{sessionId}/answer", new { questionId, selectedOption = "A" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (number < 10) questionId = body.GetProperty("nextQuestion").GetProperty("id").GetInt32();
        }

        return body.GetProperty("result");
    }

    private static async Task<JsonElement> StartAsync(HttpClient client, string phone)
    {
        using var response = await client.PostAsJsonAsync("/api/game/start", new { fullName = FullName, phoneNumber = phone });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
