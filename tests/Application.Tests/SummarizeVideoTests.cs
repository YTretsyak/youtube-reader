using Application.Tests.TestDoubles;
using Domain;
using Xunit;

namespace Application.Tests;

public class SummarizeVideoTests
{
    private static VideoUrl SampleUrl()
    {
        VideoUrl.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var url);
        return url!;
    }

    [Fact]
    public async Task ExecuteAsync_NoExistingRecord_CreatesAndPublishesAndMarksFetching()
    {
        var repository = new InMemorySummaryRepository();
        var publisher = new RecordingTranscriptRequestPublisher();
        var useCase = new SummarizeVideo(repository, publisher);
        var url = SampleUrl();

        var result = await useCase.ExecuteAsync(url);

        Assert.Equal(VideoStatus.FetchingTranscript, result.Status);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal("dQw4w9WgXcQ", publisher.PublishedMessages[0].VideoId);
        Assert.Equal(url.Value, publisher.PublishedMessages[0].VideoUrl);

        var saved = await repository.GetByVideoIdAsync(url.VideoId);
        Assert.Same(result, saved);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingProcessedRecord_ReturnsCachedWithoutPublishing()
    {
        var repository = new InMemorySummaryRepository();
        var publisher = new RecordingTranscriptRequestPublisher();
        var url = SampleUrl();

        var existing = Video.Create(url.VideoId, url);
        existing.MarkFetchingTranscript();
        existing.MarkSummarizing();
        existing.MarkProcessed("Example video", new Summary("summary text", "github-models", "openai/gpt-4o-mini"));
        await repository.SaveAsync(existing);

        var useCase = new SummarizeVideo(repository, publisher);
        var result = await useCase.ExecuteAsync(url);

        Assert.Same(existing, result);
        Assert.Equal(VideoStatus.Processed, result.Status);
        Assert.Empty(publisher.PublishedMessages);
    }

    [Theory]
    [InlineData(VideoStatus.New)]
    [InlineData(VideoStatus.FetchingTranscript)]
    [InlineData(VideoStatus.Summarizing)]
    public async Task ExecuteAsync_ExistingInFlightRecord_ReturnsExistingWithoutPublishing(VideoStatus inFlightStatus)
    {
        var repository = new InMemorySummaryRepository();
        var publisher = new RecordingTranscriptRequestPublisher();
        var url = SampleUrl();

        var existing = Video.Create(url.VideoId, url);
        if (inFlightStatus is VideoStatus.FetchingTranscript or VideoStatus.Summarizing)
            existing.MarkFetchingTranscript();
        if (inFlightStatus is VideoStatus.Summarizing)
            existing.MarkSummarizing();
        await repository.SaveAsync(existing);

        var useCase = new SummarizeVideo(repository, publisher);
        var result = await useCase.ExecuteAsync(url);

        Assert.Same(existing, result);
        Assert.Equal(inFlightStatus, result.Status);
        Assert.Empty(publisher.PublishedMessages);
    }

    [Fact]
    public async Task ExecuteAsync_ExistingFailedRecord_RequeuesAndPublishes()
    {
        var repository = new InMemorySummaryRepository();
        var publisher = new RecordingTranscriptRequestPublisher();
        var url = SampleUrl();

        var existing = Video.Create(url.VideoId, url);
        existing.MarkFetchingTranscript();
        existing.MarkFailed("no transcript available");
        await repository.SaveAsync(existing);

        var useCase = new SummarizeVideo(repository, publisher);
        var result = await useCase.ExecuteAsync(url);

        Assert.Same(existing, result);
        Assert.Equal(VideoStatus.FetchingTranscript, result.Status);
        Assert.Null(result.Error);
        Assert.Single(publisher.PublishedMessages);
        Assert.Equal("dQw4w9WgXcQ", publisher.PublishedMessages[0].VideoId);
    }
}
