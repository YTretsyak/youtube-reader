using System.Text.Json;
using Contracts;
using Infrastructure.Messaging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace TranscriptService;

public sealed class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConnectionFactory _connectionFactory;
    private readonly ITranscriptFetcher _fetcher;
    private readonly int _maxRedeliveries;

    public Worker(ILogger<Worker> logger, IConnectionFactory connectionFactory, ITranscriptFetcher fetcher, IConfiguration configuration)
    {
        _logger = logger;
        _connectionFactory = connectionFactory;
        _fetcher = fetcher;
        _maxRedeliveries = configuration.GetValue("TranscriptRequests:MaxRedeliveries", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await RabbitMqTopology.DeclareRequestsTopologyAsync(channel, stoppingToken);
        await RabbitMqTopology.DeclareResultsTopologyAsync(channel, stoppingToken);
        await RabbitMqConsumerChannel.ApplyManualAckQosAsync(channel, stoppingToken);

        var resultPublisher = new RabbitMqTranscriptResultPublisher(channel);
        var handler = new TranscriptRequestHandler(_fetcher, resultPublisher);
        var dispatcher = new ManualAckDispatcher(new RabbitMqAckChannel(channel), new ManualAckPolicy(_maxRedeliveries));

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var redeliveryCount = RabbitMqRedeliveryCount.Extract(delivery.BasicProperties);

            await dispatcher.DispatchAsync(delivery.DeliveryTag, redeliveryCount, async () =>
            {
                TranscriptRequested? message;
                try
                {
                    message = JsonSerializer.Deserialize<TranscriptRequested>(delivery.Body.Span);
                }
                catch (JsonException)
                {
                    message = null;
                }

                if (message is null)
                {
                    _logger.LogError("Received an undeserializable TranscriptRequested message; dead-lettering");
                    return DeliveryOutcome.PoisonFailure;
                }

                return await handler.HandleAsync(message, stoppingToken);
            }, stoppingToken);
        };

        await channel.BasicConsumeAsync(RabbitMqTopology.RequestsQueueName, autoAck: false, consumer, stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
