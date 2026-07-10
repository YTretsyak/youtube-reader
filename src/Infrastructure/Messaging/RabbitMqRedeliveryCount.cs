using System.Text;
using RabbitMQ.Client;

namespace Infrastructure.Messaging;

public static class RabbitMqRedeliveryCount
{
    private const string HeaderName = "x-delivery-count";

    public static long Extract(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is null || !properties.Headers.TryGetValue(HeaderName, out var value) || value is null)
            return 0;

        return value switch
        {
            long l => l,
            int i => i,
            byte[] bytes => long.Parse(Encoding.UTF8.GetString(bytes)),
            _ => Convert.ToInt64(value)
        };
    }
}
