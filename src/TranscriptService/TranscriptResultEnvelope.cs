using System.Text.Json;
using Contracts;

namespace TranscriptService;

public static class TranscriptResultEnvelope
{
    public static byte[] BuildReady(TranscriptReady message) => JsonSerializer.SerializeToUtf8Bytes(message);

    public static byte[] BuildUnavailable(TranscriptUnavailable message) => JsonSerializer.SerializeToUtf8Bytes(message);
}
