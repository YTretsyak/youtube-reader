using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public sealed class RabbitMqAckChannel : IAckChannel
{
    private readonly IChannel _channel;

    public RabbitMqAckChannel(IChannel channel)
    {
        _channel = channel;
    }

    public Task AckAsync(ulong deliveryTag, CancellationToken cancellationToken = default) =>
        _channel.BasicAckAsync(deliveryTag, multiple: false, cancellationToken).AsTask();

    public Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default) =>
        _channel.BasicNackAsync(deliveryTag, multiple: false, requeue, cancellationToken).AsTask();
}
