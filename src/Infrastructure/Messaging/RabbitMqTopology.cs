using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string RequestsQueueName = "transcript.requests";

    public static async Task DeclareRequestsTopologyAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            TranscriptRequestedEnvelope.ExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RequestsQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RequestsQueueName, TranscriptRequestedEnvelope.ExchangeName, TranscriptRequestedEnvelope.RoutingKey,
            cancellationToken: cancellationToken);
    }
}
