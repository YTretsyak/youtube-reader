using TranscriptService.Tests.TestDoubles;
using Xunit;

namespace TranscriptService.Tests;

public class YoutubeExplodeTranscriptFetcherTests
{
    [Fact]
    public async Task FetchAsync_CaptionedVideo_ReturnsSuccessWithTitleAndTranscript()
    {
        var videoClient = FakeYoutubeVideoClient.ReturningResult(
            new YoutubeVideoFetchResult("Example video", "full transcript text"));
        var fetcher = new YoutubeExplodeTranscriptFetcher(videoClient);

        var result = await fetcher.FetchAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.Equal(TranscriptFetchOutcome.Success, result.Outcome);
        Assert.Equal("Example video", result.Title);
        Assert.Equal("full transcript text", result.Transcript);
    }

    [Fact]
    public async Task FetchAsync_NoCaptionTrack_ReturnsUnavailable()
    {
        var videoClient = FakeYoutubeVideoClient.ReturningResult(
            new YoutubeVideoFetchResult("Example video", null));
        var fetcher = new YoutubeExplodeTranscriptFetcher(videoClient);

        var result = await fetcher.FetchAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.Equal(TranscriptFetchOutcome.Unavailable, result.Outcome);
        Assert.NotNull(result.Reason);
        Assert.Null(result.Transcript);
    }

    [Fact]
    public async Task FetchAsync_VideoClientThrows_ReturnsTransientError()
    {
        var videoClient = FakeYoutubeVideoClient.ThrowingException(new HttpRequestException("network error"));
        var fetcher = new YoutubeExplodeTranscriptFetcher(videoClient);

        var result = await fetcher.FetchAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.Equal(TranscriptFetchOutcome.TransientError, result.Outcome);
        Assert.Equal("network error", result.Reason);
    }

    [Fact]
    public async Task FetchAsync_VideoClientThrowsVideoUnavailable_ReturnsUnavailable()
    {
        var videoClient = FakeYoutubeVideoClient.ThrowingException(new VideoUnavailableException("video is private or deleted"));
        var fetcher = new YoutubeExplodeTranscriptFetcher(videoClient);

        var result = await fetcher.FetchAsync("https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        Assert.Equal(TranscriptFetchOutcome.Unavailable, result.Outcome);
        Assert.Equal("video is private or deleted", result.Reason);
    }
}
