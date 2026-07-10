using Contracts;
using Infrastructure.Messaging;

namespace TranscriptService;

public sealed class TranscriptRequestHandler
{
    private readonly ITranscriptFetcher _fetcher;
    private readonly ITranscriptResultPublisher _resultPublisher;

    public TranscriptRequestHandler(ITranscriptFetcher fetcher, ITranscriptResultPublisher resultPublisher)
    {
        _fetcher = fetcher;
        _resultPublisher = resultPublisher;
    }

    public async Task<DeliveryOutcome> HandleAsync(TranscriptRequested message, CancellationToken cancellationToken = default)
    {
        var result = await _fetcher.FetchAsync(message.VideoUrl, cancellationToken);

        switch (result.Outcome)
        {
            case TranscriptFetchOutcome.Success:
                await _resultPublisher.PublishReadyAsync(
                    new TranscriptReady(message.VideoId, result.Title!, result.Transcript!), cancellationToken);
                return DeliveryOutcome.Success;

            case TranscriptFetchOutcome.Unavailable:
                await _resultPublisher.PublishUnavailableAsync(
                    new TranscriptUnavailable(message.VideoId, result.Reason!), cancellationToken);
                return DeliveryOutcome.Success;

            default:
                return DeliveryOutcome.TransientFailure;
        }
    }
}
