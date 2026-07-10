using Application;
using Contracts;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitMqTranscriptRequestPublisher : ITranscriptRequestPublisher
{
    private readonly IChannel _channel;

    public RabbitMqTranscriptRequestPublisher(IChannel channel)
    {
        _channel = channel;
    }

    public async Task PublishAsync(TranscriptRequested message, CancellationToken cancellationToken = default)
    {
        var body = TranscriptRequestedEnvelope.Build(message);
        var properties = new BasicProperties { Persistent = true };

        await _channel.BasicPublishAsync(
            exchange: TranscriptRequestedEnvelope.ExchangeName,
            routingKey: TranscriptRequestedEnvelope.RoutingKey,
            body: body,
            mandatory: false,
            basicProperties: properties,
            cancellationToken: cancellationToken);
    }
}
