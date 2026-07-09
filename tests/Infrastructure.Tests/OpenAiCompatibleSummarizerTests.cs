using System.Net;
using Infrastructure.Llm;
using Infrastructure.Tests.TestDoubles;
using Xunit;

namespace Infrastructure.Tests;

public class OpenAiCompatibleSummarizerTests
{
    private static HttpClient ClientWith(FakeHttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("https://fake-llm.test/") };

    [Fact]
    public async Task SummarizeAsync_SuccessResponse_ReturnsSuccessWithSummary()
    {
        const string responseJson = """
            { "choices": [ { "message": { "role": "assistant", "content": "A short summary." } } ] }
            """;
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK, responseJson);
        var summarizer = new OpenAiCompatibleSummarizer(ClientWith(handler), "openai/gpt-4o-mini", "github-models");

        var result = await summarizer.SummarizeAsync("full transcript text");

        Assert.True(result.IsSuccess);
        Assert.Equal("A short summary.", result.Summary!.Text);
        Assert.Equal("github-models", result.Summary.Provider);
        Assert.Equal("openai/gpt-4o-mini", result.Summary.Model);
    }

    [Fact]
    public async Task SummarizeAsync_TooManyRequests_ReturnsFailure()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.TooManyRequests);
        var summarizer = new OpenAiCompatibleSummarizer(ClientWith(handler), "openai/gpt-4o-mini", "github-models");

        var result = await summarizer.SummarizeAsync("full transcript text");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task SummarizeAsync_MalformedResponseBody_ReturnsFailure()
    {
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK, "not valid json");
        var summarizer = new OpenAiCompatibleSummarizer(ClientWith(handler), "openai/gpt-4o-mini", "github-models");

        var result = await summarizer.SummarizeAsync("full transcript text");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task SummarizeAsync_EmptyChoices_ReturnsFailure()
    {
        const string responseJson = """{ "choices": [] }""";
        var handler = FakeHttpMessageHandler.ReturningStatus(HttpStatusCode.OK, responseJson);
        var summarizer = new OpenAiCompatibleSummarizer(ClientWith(handler), "openai/gpt-4o-mini", "github-models");

        var result = await summarizer.SummarizeAsync("full transcript text");

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
    }
}
