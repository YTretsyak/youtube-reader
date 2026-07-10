namespace Infrastructure.Messaging;

public sealed class ManualAckPolicy
{
    private readonly int _maxRedeliveries;

    public ManualAckPolicy(int maxRedeliveries)
    {
        if (maxRedeliveries < 0)
            throw new ArgumentOutOfRangeException(nameof(maxRedeliveries), "Max redeliveries cannot be negative.");

        _maxRedeliveries = maxRedeliveries;
    }

    public AckDecision Decide(DeliveryOutcome outcome, long redeliveryCount) => outcome switch
    {
        DeliveryOutcome.Success => AckDecision.Ack,
        DeliveryOutcome.PoisonFailure => AckDecision.DeadLetter,
        DeliveryOutcome.TransientFailure when redeliveryCount >= _maxRedeliveries => AckDecision.DeadLetter,
        DeliveryOutcome.TransientFailure => AckDecision.RequeueRetry,
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
