namespace Infrastructure.Messaging;

public sealed class ManualAckDispatcher
{
    private readonly IAckChannel _channel;
    private readonly ManualAckPolicy _policy;

    public ManualAckDispatcher(IAckChannel channel, ManualAckPolicy policy)
    {
        _channel = channel;
        _policy = policy;
    }

    public async Task<AckDecision> DispatchAsync(
        ulong deliveryTag,
        long redeliveryCount,
        Func<Task<DeliveryOutcome>> handleAsync,
        CancellationToken cancellationToken = default)
    {
        var outcome = await handleAsync();
        var decision = _policy.Decide(outcome, redeliveryCount);

        switch (decision)
        {
            case AckDecision.Ack:
                await _channel.AckAsync(deliveryTag, cancellationToken);
                break;
            case AckDecision.RequeueRetry:
                await _channel.NackAsync(deliveryTag, requeue: true, cancellationToken);
                break;
            case AckDecision.DeadLetter:
                await _channel.NackAsync(deliveryTag, requeue: false, cancellationToken);
                break;
        }

        return decision;
    }
}
