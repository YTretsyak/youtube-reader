using Domain;
using Xunit;

namespace Domain.Tests;

public class VideoTests
{
    private static VideoUrl SampleUrl()
    {
        VideoUrl.TryParse("https://www.youtube.com/watch?v=dQw4w9WgXcQ", out var url);
        return url!;
    }

    private static Video NewVideo() => Video.Create(new VideoId("dQw4w9WgXcQ"), SampleUrl());

    [Fact]
    public void Create_ReturnsVideoWithNewStatus()
    {
        var video = NewVideo();

        Assert.Equal(VideoStatus.New, video.Status);
        Assert.Null(video.Title);
        Assert.Null(video.Summary);
        Assert.Null(video.Error);
    }

    [Fact]
    public void FullLifecycle_NewToProcessed_TransitionsAndFieldsAreCorrect()
    {
        var video = NewVideo();

        video.MarkFetchingTranscript();
        Assert.Equal(VideoStatus.FetchingTranscript, video.Status);

        video.MarkSummarizing();
        Assert.Equal(VideoStatus.Summarizing, video.Status);

        var summary = new Summary("a short summary", "github-models", "openai/gpt-4o-mini");
        video.MarkProcessed("Example video", summary);

        Assert.Equal(VideoStatus.Processed, video.Status);
        Assert.Equal("Example video", video.Title);
        Assert.Same(summary, video.Summary);
        Assert.Null(video.Error);
    }

    [Fact]
    public void MarkFailed_FromFetchingTranscript_SetsFailedAndError()
    {
        var video = NewVideo();
        video.MarkFetchingTranscript();

        video.MarkFailed("no transcript available");

        Assert.Equal(VideoStatus.Failed, video.Status);
        Assert.Equal("no transcript available", video.Error);
    }

    [Fact]
    public void MarkFailed_FromSummarizing_SetsFailedAndError()
    {
        var video = NewVideo();
        video.MarkFetchingTranscript();
        video.MarkSummarizing();

        video.MarkFailed("LLM provider error");

        Assert.Equal(VideoStatus.Failed, video.Status);
        Assert.Equal("LLM provider error", video.Error);
    }

    [Fact]
    public void Resubmit_FromFailed_ResetsToNewAndClearsError()
    {
        var video = NewVideo();
        video.MarkFetchingTranscript();
        video.MarkFailed("no transcript available");

        video.Resubmit();

        Assert.Equal(VideoStatus.New, video.Status);
        Assert.Null(video.Error);
    }

    [Theory]
    [InlineData(VideoStatus.FetchingTranscript)]
    [InlineData(VideoStatus.Summarizing)]
    [InlineData(VideoStatus.Processed)]
    [InlineData(VideoStatus.Failed)]
    public void MarkFetchingTranscript_FromNonNewStatus_Throws(VideoStatus startStatus)
    {
        var video = ToStatus(startStatus);

        Assert.Throws<InvalidOperationException>(video.MarkFetchingTranscript);
    }

    [Theory]
    [InlineData(VideoStatus.New)]
    [InlineData(VideoStatus.Summarizing)]
    [InlineData(VideoStatus.Processed)]
    [InlineData(VideoStatus.Failed)]
    public void MarkSummarizing_FromNonFetchingTranscriptStatus_Throws(VideoStatus startStatus)
    {
        var video = ToStatus(startStatus);

        Assert.Throws<InvalidOperationException>(video.MarkSummarizing);
    }

    [Theory]
    [InlineData(VideoStatus.New)]
    [InlineData(VideoStatus.FetchingTranscript)]
    [InlineData(VideoStatus.Processed)]
    [InlineData(VideoStatus.Failed)]
    public void MarkProcessed_FromNonSummarizingStatus_Throws(VideoStatus startStatus)
    {
        var video = ToStatus(startStatus);
        var summary = new Summary("text", "provider", "model");

        Assert.Throws<InvalidOperationException>(() => video.MarkProcessed("title", summary));
    }

    [Theory]
    [InlineData(VideoStatus.New)]
    [InlineData(VideoStatus.Processed)]
    [InlineData(VideoStatus.Failed)]
    public void MarkFailed_FromNonInFlightStatus_Throws(VideoStatus startStatus)
    {
        var video = ToStatus(startStatus);

        Assert.Throws<InvalidOperationException>(() => video.MarkFailed("error"));
    }

    [Theory]
    [InlineData(VideoStatus.New)]
    [InlineData(VideoStatus.FetchingTranscript)]
    [InlineData(VideoStatus.Summarizing)]
    [InlineData(VideoStatus.Processed)]
    public void Resubmit_FromNonFailedStatus_Throws(VideoStatus startStatus)
    {
        var video = ToStatus(startStatus);

        Assert.Throws<InvalidOperationException>(video.Resubmit);
    }

    private static Video ToStatus(VideoStatus status)
    {
        var video = NewVideo();
        if (status == VideoStatus.New) return video;

        video.MarkFetchingTranscript();
        if (status == VideoStatus.FetchingTranscript) return video;

        if (status == VideoStatus.Failed)
        {
            video.MarkFailed("error");
            return video;
        }

        video.MarkSummarizing();
        if (status == VideoStatus.Summarizing) return video;

        video.MarkProcessed("title", new Summary("text", "provider", "model"));
        return video; // Processed
    }
}
