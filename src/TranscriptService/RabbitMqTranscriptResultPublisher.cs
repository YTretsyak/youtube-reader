using Contracts;
using Infrastructure.Messaging;
using RabbitMQ.Client;

namespace TranscriptService;

public sealed class RabbitMqTranscriptResultPublisher : ITranscriptResultPublisher
{
    private readonly IChannel _channel;

    public RabbitMqTranscriptResultPublisher(IChannel channel)
    {
        _channel = channel;
    }

    public Task PublishReadyAsync(TranscriptReady message, CancellationToken cancellationToken = default) =>
        PublishAsync(RabbitMqTopology.TranscriptReadyRoutingKey, TranscriptResultEnvelope.BuildReady(message), cancellationToken);

    public Task PublishUnavailableAsync(TranscriptUnavailable message, CancellationToken cancellationToken = default) =>
        PublishAsync(RabbitMqTopology.TranscriptUnavailableRoutingKey, TranscriptResultEnvelope.BuildUnavailable(message), cancellationToken);

    private async Task PublishAsync(string routingKey, byte[] body, CancellationToken cancellationToken)
    {
        var properties = new BasicProperties { Persistent = true };

        await _channel.BasicPublishAsync(
            exchange: RabbitMqTopology.ResultsExchangeName,
            routingKey: routingKey,
            body: body,
            mandatory: false,
            basicProperties: properties,
            cancellationToken: cancellationToken);
    }
}
