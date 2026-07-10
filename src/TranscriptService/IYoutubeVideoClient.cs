namespace TranscriptService;

public interface IYoutubeVideoClient
{
    Task<YoutubeVideoFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default);
}
