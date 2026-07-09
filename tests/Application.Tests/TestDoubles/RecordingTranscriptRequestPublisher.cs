using Contracts;

namespace Application.Tests.TestDoubles;

public sealed class RecordingTranscriptRequestPublisher : ITranscriptRequestPublisher
{
    public List<TranscriptRequested> PublishedMessages { get; } = new();

    public Task PublishAsync(TranscriptRequested message, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }
}
