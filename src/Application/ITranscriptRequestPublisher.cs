using Contracts;

namespace Application;

public interface ITranscriptRequestPublisher
{
    Task PublishAsync(TranscriptRequested message, CancellationToken cancellationToken = default);
}
