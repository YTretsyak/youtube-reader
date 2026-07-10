using Contracts;

namespace TranscriptService.Tests.TestDoubles;

public sealed class RecordingTranscriptResultPublisher : ITranscriptResultPublisher
{
    public List<TranscriptReady> ReadyMessages { get; } = new();
    public List<TranscriptUnavailable> UnavailableMessages { get; } = new();

    public Task PublishReadyAsync(TranscriptReady message, CancellationToken cancellationToken = default)
    {
        ReadyMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task PublishUnavailableAsync(TranscriptUnavailable message, CancellationToken cancellationToken = default)
    {
        UnavailableMessages.Add(message);
        return Task.CompletedTask;
    }
}
