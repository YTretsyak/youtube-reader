using Infrastructure.Messaging;
using RabbitMQ.Client;
using Xunit;

namespace Infrastructure.Tests;

public class RabbitMqRedeliveryCountTests
{
    [Fact]
    public void Extract_NoHeaders_ReturnsZero()
    {
        var properties = new BasicProperties();

        var count = RabbitMqRedeliveryCount.Extract(properties);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Extract_LongHeaderValue_ReturnsValue()
    {
        var properties = new BasicProperties { Headers = new Dictionary<string, object> { ["x-delivery-count"] = 3L } };

        var count = RabbitMqRedeliveryCount.Extract(properties);

        Assert.Equal(3, count);
    }

    [Fact]
    public void Extract_IntHeaderValue_ReturnsValue()
    {
        var properties = new BasicProperties { Headers = new Dictionary<string, object> { ["x-delivery-count"] = 2 } };

        var count = RabbitMqRedeliveryCount.Extract(properties);

        Assert.Equal(2, count);
    }

    [Fact]
    public void Extract_ByteArrayHeaderValue_ReturnsValue()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes("5");
        var properties = new BasicProperties { Headers = new Dictionary<string, object> { ["x-delivery-count"] = bytes } };

        var count = RabbitMqRedeliveryCount.Extract(properties);

        Assert.Equal(5, count);
    }
}
