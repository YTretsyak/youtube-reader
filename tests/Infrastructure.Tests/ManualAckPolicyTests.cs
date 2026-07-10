using Infrastructure.Messaging;
using Xunit;

namespace Infrastructure.Tests;

public class ManualAckPolicyTests
{
    [Fact]
    public void Decide_Success_ReturnsAck()
    {
        var policy = new ManualAckPolicy(maxRedeliveries: 3);

        var decision = policy.Decide(DeliveryOutcome.Success, redeliveryCount: 0);

        Assert.Equal(AckDecision.Ack, decision);
    }

    [Fact]
    public void Decide_PoisonFailure_ReturnsDeadLetterRegardlessOfRedeliveryCount()
    {
        var policy = new ManualAckPolicy(maxRedeliveries: 3);

        var decision = policy.Decide(DeliveryOutcome.PoisonFailure, redeliveryCount: 0);

        Assert.Equal(AckDecision.DeadLetter, decision);
    }

    [Fact]
    public void Decide_TransientFailureBelowCap_ReturnsRequeueRetry()
    {
        var policy = new ManualAckPolicy(maxRedeliveries: 3);

        var decision = policy.Decide(DeliveryOutcome.TransientFailure, redeliveryCount: 2);

        Assert.Equal(AckDecision.RequeueRetry, decision);
    }

    [Fact]
    public void Decide_TransientFailureAtCap_ReturnsDeadLetter()
    {
        var policy = new ManualAckPolicy(maxRedeliveries: 3);

        var decision = policy.Decide(DeliveryOutcome.TransientFailure, redeliveryCount: 3);

        Assert.Equal(AckDecision.DeadLetter, decision);
    }

    [Fact]
    public void Constructor_NegativeMaxRedeliveries_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ManualAckPolicy(maxRedeliveries: -1));
    }
}
