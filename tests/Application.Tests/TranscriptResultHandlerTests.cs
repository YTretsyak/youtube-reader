using Application.Tests.TestDoubles;
using Contracts;
using Domain;
using Xunit;

namespace Application.Tests;

public class TranscriptResultHandlerTests
{
    private static VideoUrl SampleUrl()
    {
        VideoUrl.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var url);
        return url!;
    }

    private static async Task<(InMemorySummaryRepository repository, Video video)> SeedFetchingVideoAsync()
    {
        var repository = new InMemorySummaryRepository();
        var url = SampleUrl();
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        await repository.SaveAsync(video);
        return (repository, video);
    }

    [Fact]
    public async Task HandleReadyAsync_SummarizerSucceeds_MarksProcessed()
    {
        var (repository, video) = await SeedFetchingVideoAsync();
        var summary = new Summary("summary text", "github-models", "openai/gpt-4o-mini");
        var summarizer = new StubSummarizer(SummarizerResult.Success(summary));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var message = new TranscriptReady(video.Id.Value, "Example video", "full transcript text");

        await handler.HandleReadyAsync(message);

        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Processed, updated!.Status);
        Assert.Equal("Example video", updated.Title);
        Assert.Same(summary, updated.Summary);
    }

    [Fact]
    public async Task HandleReadyAsync_SummarizerFails_MarksFailed()
    {
        var (repository, video) = await SeedFetchingVideoAsync();
        var summarizer = new StubSummarizer(SummarizerResult.Failure("provider rate limited"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var message = new TranscriptReady(video.Id.Value, "Example video", "full transcript text");

        await handler.HandleReadyAsync(message);

        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Failed, updated!.Status);
        Assert.Equal("provider rate limited", updated.Error);
    }

    [Fact]
    public async Task HandleReadyAsync_VideoAlreadyProcessed_IsNoOp()
    {
        var (repository, video) = await SeedFetchingVideoAsync();
        video.MarkSummarizing();
        var originalSummary = new Summary("original summary", "github-models", "openai/gpt-4o-mini");
        video.MarkProcessed("Original title", originalSummary);
        await repository.SaveAsync(video);

        var summarizer = new StubSummarizer(SummarizerResult.Success(new Summary("new summary", "github-models", "openai/gpt-4o-mini")));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var message = new TranscriptReady(video.Id.Value, "New title", "full transcript text");

        await handler.HandleReadyAsync(message);

        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Processed, updated!.Status);
        Assert.Equal("Original title", updated.Title);
        Assert.Same(originalSummary, updated.Summary);
    }

    [Fact]
    public async Task HandleUnavailableAsync_MarksFailedWithReason()
    {
        var (repository, video) = await SeedFetchingVideoAsync();
        var summarizer = new StubSummarizer(SummarizerResult.Failure("should not be called"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var message = new TranscriptUnavailable(video.Id.Value, "no captions available");

        await handler.HandleUnavailableAsync(message);

        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Failed, updated!.Status);
        Assert.Equal("no captions available", updated.Error);
    }

    [Fact]
    public async Task HandleUnavailableAsync_VideoAlreadyFailed_IsNoOp()
    {
        var (repository, video) = await SeedFetchingVideoAsync();
        video.MarkFailed("first failure reason");
        await repository.SaveAsync(video);

        var summarizer = new StubSummarizer(SummarizerResult.Failure("should not be called"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var message = new TranscriptUnavailable(video.Id.Value, "second failure reason");

        await handler.HandleUnavailableAsync(message);

        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Failed, updated!.Status);
        Assert.Equal("first failure reason", updated.Error);
    }
}
