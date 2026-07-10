using System.Text.Json;
using Contracts;
using Xunit;

namespace TranscriptService.Tests;

public class TranscriptResultEnvelopeTests
{
    [Fact]
    public void BuildReady_SerializesVideoIdTitleAndTranscript()
    {
        var message = new TranscriptReady("dQw4w9WgXcQ", "Example video", "full transcript text");

        var body = TranscriptResultEnvelope.BuildReady(message);
        var roundTripped = JsonSerializer.Deserialize<TranscriptReady>(body);

        Assert.Equal(message.VideoId, roundTripped!.VideoId);
        Assert.Equal(message.Title, roundTripped.Title);
        Assert.Equal(message.Transcript, roundTripped.Transcript);
    }

    [Fact]
    public void BuildUnavailable_SerializesVideoIdAndReason()
    {
        var message = new TranscriptUnavailable("dQw4w9WgXcQ", "no transcript available");

        var body = TranscriptResultEnvelope.BuildUnavailable(message);
        var roundTripped = JsonSerializer.Deserialize<TranscriptUnavailable>(body);

        Assert.Equal(message.VideoId, roundTripped!.VideoId);
        Assert.Equal(message.Reason, roundTripped.Reason);
    }
}
