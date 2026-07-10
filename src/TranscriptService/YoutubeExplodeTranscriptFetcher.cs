namespace TranscriptService;

public sealed class YoutubeExplodeTranscriptFetcher : ITranscriptFetcher
{
    private readonly IYoutubeVideoClient _videoClient;

    public YoutubeExplodeTranscriptFetcher(IYoutubeVideoClient videoClient)
    {
        _videoClient = videoClient;
    }

    public async Task<TranscriptFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _videoClient.FetchAsync(videoUrl, cancellationToken);

            return result.TranscriptText is null
                ? TranscriptFetchResult.Unavailable("no transcript available")
                : TranscriptFetchResult.Success(result.Title, result.TranscriptText);
        }
        catch (Exception ex)
        {
            return TranscriptFetchResult.TransientError(ex.Message);
        }
    }
}
