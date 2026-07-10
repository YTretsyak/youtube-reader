using Contracts;

namespace TranscriptService;

public interface ITranscriptResultPublisher
{
    Task PublishReadyAsync(TranscriptReady message, CancellationToken cancellationToken = default);

    Task PublishUnavailableAsync(TranscriptUnavailable message, CancellationToken cancellationToken = default);
}
