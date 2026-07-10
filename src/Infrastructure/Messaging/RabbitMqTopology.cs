using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public static class RabbitMqTopology
{
    public const string RequestsQueueName = "transcript.requests";
    public const string RequestsDeadLetterExchangeName = "transcript.requests.dead-letter";
    public const string RequestsDeadLetterQueueName = "transcript.requests.dead-letter";

    public const string ResultsExchangeName = "transcript.results";
    public const string ResultsQueueName = "transcript.results";
    public const string TranscriptReadyRoutingKey = "transcript.ready";
    public const string TranscriptUnavailableRoutingKey = "transcript.unavailable";

    public static async Task DeclareRequestsTopologyAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            RequestsDeadLetterExchangeName, ExchangeType.Fanout, durable: true, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            RequestsDeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RequestsDeadLetterQueueName, RequestsDeadLetterExchangeName, routingKey: "", cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            TranscriptRequestedEnvelope.ExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        // Quorum (not classic) so the broker stamps redelivered messages with an
        // `x-delivery-count` header — RabbitMqRedeliveryCount.Extract needs a real count for
        // ManualAckPolicy's retry cap; a classic queue only exposes a Redelivered bool.
        var requestsQueueArguments = new Dictionary<string, object>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = RequestsDeadLetterExchangeName
        };

        await channel.QueueDeclareAsync(
            RequestsQueueName, durable: true, exclusive: false, autoDelete: false,
            arguments: requestsQueueArguments, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            RequestsQueueName, TranscriptRequestedEnvelope.ExchangeName, TranscriptRequestedEnvelope.RoutingKey,
            cancellationToken: cancellationToken);
    }

    public static async Task DeclareResultsTopologyAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        await channel.ExchangeDeclareAsync(
            ResultsExchangeName, ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            ResultsQueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            ResultsQueueName, ResultsExchangeName, TranscriptReadyRoutingKey, cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            ResultsQueueName, ResultsExchangeName, TranscriptUnavailableRoutingKey, cancellationToken: cancellationToken);
    }
}
