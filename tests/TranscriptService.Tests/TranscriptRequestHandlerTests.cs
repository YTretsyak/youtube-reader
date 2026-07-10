using Contracts;
using Infrastructure.Messaging;
using TranscriptService.Tests.TestDoubles;
using Xunit;

namespace TranscriptService.Tests;

public class TranscriptRequestHandlerTests
{
    private static TranscriptRequested SampleMessage() =>
        new("dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

    [Fact]
    public async Task HandleAsync_CaptionedVideo_PublishesReadyAndReturnsSuccess()
    {
        var fetcher = new StubTranscriptFetcher(TranscriptFetchResult.Success("Example video", "full transcript text"));
        var publisher = new RecordingTranscriptResultPublisher();
        var handler = new TranscriptRequestHandler(fetcher, publisher);

        var outcome = await handler.HandleAsync(SampleMessage());

        Assert.Equal(DeliveryOutcome.Success, outcome);
        Assert.Single(publisher.ReadyMessages);
        Assert.Equal("dQw4w9WgXcQ", publisher.ReadyMessages[0].VideoId);
        Assert.Equal("Example video", publisher.ReadyMessages[0].Title);
        Assert.Equal("full transcript text", publisher.ReadyMessages[0].Transcript);
        Assert.Empty(publisher.UnavailableMessages);
    }

    [Fact]
    public async Task HandleAsync_NoCaptionTrack_PublishesUnavailableAndReturnsSuccess()
    {
        var fetcher = new StubTranscriptFetcher(TranscriptFetchResult.Unavailable("no transcript available"));
        var publisher = new RecordingTranscriptResultPublisher();
        var handler = new TranscriptRequestHandler(fetcher, publisher);

        var outcome = await handler.HandleAsync(SampleMessage());

        Assert.Equal(DeliveryOutcome.Success, outcome);
        Assert.Single(publisher.UnavailableMessages);
        Assert.Equal("dQw4w9WgXcQ", publisher.UnavailableMessages[0].VideoId);
        Assert.Equal("no transcript available", publisher.UnavailableMessages[0].Reason);
        Assert.Empty(publisher.ReadyMessages);
    }

    [Fact]
    public async Task HandleAsync_TransientError_PublishesNothingAndReturnsTransientFailure()
    {
        var fetcher = new StubTranscriptFetcher(TranscriptFetchResult.TransientError("network error"));
        var publisher = new RecordingTranscriptResultPublisher();
        var handler = new TranscriptRequestHandler(fetcher, publisher);

        var outcome = await handler.HandleAsync(SampleMessage());

        Assert.Equal(DeliveryOutcome.TransientFailure, outcome);
        Assert.Empty(publisher.ReadyMessages);
        Assert.Empty(publisher.UnavailableMessages);
    }
}
