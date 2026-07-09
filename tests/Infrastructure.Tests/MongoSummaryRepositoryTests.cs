using Domain;
using Infrastructure.Persistence;
using Infrastructure.Tests.TestDoubles;
using Xunit;

namespace Infrastructure.Tests;

public class MongoSummaryRepositoryTests
{
    private static VideoUrl SampleUrl()
    {
        VideoUrl.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var url);
        return url!;
    }

    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }

    [Fact]
    public async Task GetByVideoIdAsync_NoDocument_ReturnsNull()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());

        var result = await repository.GetByVideoIdAsync(new VideoId("dQw4w9WgXcQ"));

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenGetByVideoIdAsync_RoundTripsNewVideo()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());
        var url = SampleUrl();
        var video = Video.Create(url.VideoId, url);

        await repository.SaveAsync(video);
        var loaded = await repository.GetByVideoIdAsync(url.VideoId);

        Assert.NotNull(loaded);
        Assert.Equal(VideoStatus.New, loaded!.Status);
        Assert.Equal(url.VideoId.Value, loaded.Id.Value);
        Assert.Equal(url.Value, loaded.Url.Value);
    }

    [Fact]
    public async Task SaveAsync_ProcessedVideo_RoundTripsTitleAndSummary()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());
        var url = SampleUrl();
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        video.MarkSummarizing();
        video.MarkProcessed("Example video", new Summary("summary text", "github-models", "openai/gpt-4o-mini"));

        await repository.SaveAsync(video);
        var loaded = await repository.GetByVideoIdAsync(url.VideoId);

        Assert.Equal(VideoStatus.Processed, loaded!.Status);
        Assert.Equal("Example video", loaded.Title);
        Assert.Equal("summary text", loaded.Summary!.Text);
        Assert.Equal("github-models", loaded.Summary.Provider);
        Assert.Equal("openai/gpt-4o-mini", loaded.Summary.Model);
    }

    [Fact]
    public async Task SaveAsync_FailedVideo_RoundTripsError()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());
        var url = SampleUrl();
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        video.MarkFailed("no transcript available");

        await repository.SaveAsync(video);
        var loaded = await repository.GetByVideoIdAsync(url.VideoId);

        Assert.Equal(VideoStatus.Failed, loaded!.Status);
        Assert.Equal("no transcript available", loaded.Error);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsNewestFirst()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());

        var first = Video.Create(new VideoId("aaaaaaaaaaa"), ParseUrl("https://youtu.be/aaaaaaaaaaa"));
        await repository.SaveAsync(first);
        var second = Video.Create(new VideoId("bbbbbbbbbbb"), ParseUrl("https://youtu.be/bbbbbbbbbbb"));
        await repository.SaveAsync(second);

        var history = await repository.GetHistoryAsync();

        Assert.Equal(new[] { "bbbbbbbbbbb", "aaaaaaaaaaa" }, history.Select(v => v.Id.Value));
    }

    [Fact]
    public async Task GetHistoryAsync_UpdatingAnExistingVideo_DoesNotChangeItsPosition()
    {
        var repository = new MongoSummaryRepository(new InMemoryVideoDocumentStore());

        var first = Video.Create(new VideoId("aaaaaaaaaaa"), ParseUrl("https://youtu.be/aaaaaaaaaaa"));
        await repository.SaveAsync(first);
        var second = Video.Create(new VideoId("bbbbbbbbbbb"), ParseUrl("https://youtu.be/bbbbbbbbbbb"));
        await repository.SaveAsync(second);

        first.MarkFetchingTranscript();
        await repository.SaveAsync(first);

        var history = await repository.GetHistoryAsync();

        Assert.Equal(new[] { "bbbbbbbbbbb", "aaaaaaaaaaa" }, history.Select(v => v.Id.Value));
    }
}
