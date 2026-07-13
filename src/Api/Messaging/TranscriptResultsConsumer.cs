using System.Text.Json;
using Application;
using Contracts;
using Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Api.Messaging;

public sealed class TranscriptResultsConsumer : BackgroundService
{
    private readonly ILogger<TranscriptResultsConsumer> _logger;
    private readonly IConnection _connection;
    private readonly TranscriptResultHandler _handler;
    private readonly ManualAckPolicy _ackPolicy;

    public TranscriptResultsConsumer(
        ILogger<TranscriptResultsConsumer> logger,
        IConnection connection,
        TranscriptResultHandler handler,
        ManualAckPolicy ackPolicy)
    {
        _logger = logger;
        _connection = connection;
        _handler = handler;
        _ackPolicy = ackPolicy;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareResultsTopologyAsync(channel, stoppingToken);
        await RabbitMqConsumerChannel.ApplyManualAckQosAsync(channel, stoppingToken);

        var dispatcher = new ManualAckDispatcher(new RabbitMqAckChannel(channel), _ackPolicy);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var redeliveryCount = RabbitMqRedeliveryCount.Extract(delivery.BasicProperties);

            await dispatcher.DispatchAsync(delivery.DeliveryTag, redeliveryCount, async () =>
                await HandleDeliveryAsync(delivery, stoppingToken), stoppingToken);
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.ResultsQueueName, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    internal async Task<DeliveryOutcome> HandleDeliveryAsync(BasicDeliverEventArgs delivery, CancellationToken cancellationToken)
    {
        try
        {
            if (delivery.RoutingKey == RabbitMqTopology.TranscriptReadyRoutingKey)
            {
                var message = JsonSerializer.Deserialize<TranscriptReady>(delivery.Body.Span);
                if (message is null)
                {
                    _logger.LogError("Received an undeserializable TranscriptReady message; dead-lettering");
                    return DeliveryOutcome.PoisonFailure;
                }

                await _handler.HandleReadyAsync(message, cancellationToken);
                return DeliveryOutcome.Success;
            }

            if (delivery.RoutingKey == RabbitMqTopology.TranscriptUnavailableRoutingKey)
            {
                var message = JsonSerializer.Deserialize<TranscriptUnavailable>(delivery.Body.Span);
                if (message is null)
                {
                    _logger.LogError("Received an undeserializable TranscriptUnavailable message; dead-lettering");
                    return DeliveryOutcome.PoisonFailure;
                }

                await _handler.HandleUnavailableAsync(message, cancellationToken);
                return DeliveryOutcome.Success;
            }

            _logger.LogError("Received a result message with unknown routing key {RoutingKey}; dead-lettering", delivery.RoutingKey);
            return DeliveryOutcome.PoisonFailure;
        }
        catch (JsonException)
        {
            _logger.LogError("Received an undeserializable transcript result message; dead-lettering");
            return DeliveryOutcome.PoisonFailure;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Transient failure handling a transcript result; it will be redelivered");
            return DeliveryOutcome.TransientFailure;
        }
    }
}
