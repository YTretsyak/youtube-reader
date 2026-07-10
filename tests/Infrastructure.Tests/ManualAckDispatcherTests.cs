using Infrastructure.Messaging;
using Infrastructure.Tests.TestDoubles;
using Xunit;

namespace Infrastructure.Tests;

public class ManualAckDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_HandlerSucceeds_AcksMessage()
    {
        var channel = new RecordingAckChannel();
        var dispatcher = new ManualAckDispatcher(channel, new ManualAckPolicy(maxRedeliveries: 3));

        var decision = await dispatcher.DispatchAsync(deliveryTag: 1, redeliveryCount: 0, () => Task.FromResult(DeliveryOutcome.Success));

        Assert.Equal(AckDecision.Ack, decision);
        Assert.Equal(new ulong[] { 1 }, channel.AckedMessages);
        Assert.Empty(channel.NackedMessages);
    }

    [Fact]
    public async Task DispatchAsync_TransientFailureBelowCap_NacksWithRequeue()
    {
        var channel = new RecordingAckChannel();
        var dispatcher = new ManualAckDispatcher(channel, new ManualAckPolicy(maxRedeliveries: 3));

        var decision = await dispatcher.DispatchAsync(deliveryTag: 2, redeliveryCount: 1, () => Task.FromResult(DeliveryOutcome.TransientFailure));

        Assert.Equal(AckDecision.RequeueRetry, decision);
        Assert.Equal((2ul, true), Assert.Single(channel.NackedMessages));
        Assert.Empty(channel.AckedMessages);
    }

    [Fact]
    public async Task DispatchAsync_TransientFailureAtCap_NacksWithoutRequeue()
    {
        var channel = new RecordingAckChannel();
        var dispatcher = new ManualAckDispatcher(channel, new ManualAckPolicy(maxRedeliveries: 3));

        var decision = await dispatcher.DispatchAsync(deliveryTag: 3, redeliveryCount: 3, () => Task.FromResult(DeliveryOutcome.TransientFailure));

        Assert.Equal(AckDecision.DeadLetter, decision);
        Assert.Equal((3ul, false), Assert.Single(channel.NackedMessages));
        Assert.Empty(channel.AckedMessages);
    }

    [Fact]
    public async Task DispatchAsync_PoisonFailure_NacksWithoutRequeueRegardlessOfCount()
    {
        var channel = new RecordingAckChannel();
        var dispatcher = new ManualAckDispatcher(channel, new ManualAckPolicy(maxRedeliveries: 3));

        var decision = await dispatcher.DispatchAsync(deliveryTag: 4, redeliveryCount: 0, () => Task.FromResult(DeliveryOutcome.PoisonFailure));

        Assert.Equal(AckDecision.DeadLetter, decision);
        Assert.Equal((4ul, false), Assert.Single(channel.NackedMessages));
    }
}
