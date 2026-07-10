using YoutubeExplode;

namespace TranscriptService;

public sealed class YoutubeExplodeVideoClient : IYoutubeVideoClient
{
    private const string EnglishLanguageCode = "en";

    private readonly HttpClient _httpClient;

    public YoutubeExplodeVideoClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<YoutubeVideoFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default)
    {
        // Not `using var youtube = ...` — YoutubeClient.Dispose() would tear down the
        // IHttpClientFactory-managed HttpClient it wraps, breaking reuse across requests.
        var youtube = new YoutubeClient(_httpClient);

        var video = await youtube.Videos.GetAsync(videoUrl, cancellationToken);
        var manifest = await youtube.Videos.ClosedCaptions.GetManifestAsync(videoUrl, cancellationToken);
        var trackInfo = manifest.Tracks.FirstOrDefault(t => t.Language.Code == EnglishLanguageCode);

        if (trackInfo is null)
            return new YoutubeVideoFetchResult(video.Title, null);

        var track = await youtube.Videos.ClosedCaptions.GetAsync(trackInfo, cancellationToken);
        var transcript = string.Join(" ", track.Captions.Select(c => c.Text));

        return new YoutubeVideoFetchResult(video.Title, transcript);
    }
}
