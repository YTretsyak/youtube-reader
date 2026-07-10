using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public static class RabbitMqConsumerChannel
{
    public const ushort PrefetchCount = 1;

    public static Task ApplyManualAckQosAsync(IChannel channel, CancellationToken cancellationToken = default) =>
        channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, cancellationToken);
}
