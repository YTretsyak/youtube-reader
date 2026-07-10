using System.Text.Json;
using Contracts;
using Infrastructure.Messaging;
using Xunit;

namespace Infrastructure.Tests;

public class TranscriptRequestedEnvelopeTests
{
    [Fact]
    public void Build_SerializesVideoIdAndVideoUrl()
    {
        var message = new TranscriptRequested("dQw4w9WgXcQ", "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        var body = TranscriptRequestedEnvelope.Build(message);
        var roundTripped = JsonSerializer.Deserialize<TranscriptRequested>(body);

        Assert.Equal(message.VideoId, roundTripped!.VideoId);
        Assert.Equal(message.VideoUrl, roundTripped.VideoUrl);
    }
}
