using Application;
using Contracts;

namespace Api.Tests.TestDoubles;

public sealed class RecordingTranscriptRequestPublisher : ITranscriptRequestPublisher
{
    public List<TranscriptRequested> PublishedMessages { get; } = new();

    public Task PublishAsync(TranscriptRequested message, CancellationToken cancellationToken = default)
    {
        PublishedMessages.Add(message);
        return Task.CompletedTask;
    }
}
