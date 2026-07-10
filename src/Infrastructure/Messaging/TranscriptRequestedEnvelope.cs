using System.Text.Json;
using Contracts;

namespace Infrastructure.Messaging;

public static class TranscriptRequestedEnvelope
{
    public const string ExchangeName = "transcript.requests";
    public const string RoutingKey = "";

    public static byte[] Build(TranscriptRequested message) => JsonSerializer.SerializeToUtf8Bytes(message);
}
