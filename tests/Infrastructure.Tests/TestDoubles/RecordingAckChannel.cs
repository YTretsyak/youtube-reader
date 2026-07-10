using Infrastructure.Messaging;

namespace Infrastructure.Tests.TestDoubles;

public sealed class RecordingAckChannel : IAckChannel
{
    public List<ulong> AckedMessages { get; } = new();
    public List<(ulong DeliveryTag, bool Requeue)> NackedMessages { get; } = new();

    public Task AckAsync(ulong deliveryTag, CancellationToken cancellationToken = default)
    {
        AckedMessages.Add(deliveryTag);
        return Task.CompletedTask;
    }

    public Task NackAsync(ulong deliveryTag, bool requeue, CancellationToken cancellationToken = default)
    {
        NackedMessages.Add((deliveryTag, requeue));
        return Task.CompletedTask;
    }
}
