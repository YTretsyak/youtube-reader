namespace Infrastructure.Messaging;

public interface IAckChannel
{
    Task AckAsync(ulong deliveryTag, CancellationToken cancellationToken = default);

    Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default);
}
