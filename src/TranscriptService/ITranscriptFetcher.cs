namespace TranscriptService;

public interface ITranscriptFetcher
{
    Task<TranscriptFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default);
}
