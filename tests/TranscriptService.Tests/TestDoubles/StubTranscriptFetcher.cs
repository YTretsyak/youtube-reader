namespace TranscriptService.Tests.TestDoubles;

public sealed class StubTranscriptFetcher : ITranscriptFetcher
{
    private readonly TranscriptFetchResult _result;

    public StubTranscriptFetcher(TranscriptFetchResult result)
    {
        _result = result;
    }

    public Task<TranscriptFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(_result);
}
