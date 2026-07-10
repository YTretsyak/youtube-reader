namespace TranscriptService.Tests.TestDoubles;

public sealed class FakeYoutubeVideoClient : IYoutubeVideoClient
{
    private readonly Func<string, YoutubeVideoFetchResult> _respond;

    public FakeYoutubeVideoClient(Func<string, YoutubeVideoFetchResult> respond)
    {
        _respond = respond;
    }

    public static FakeYoutubeVideoClient ReturningResult(YoutubeVideoFetchResult result) => new(_ => result);

    public static FakeYoutubeVideoClient ThrowingException(Exception exception) =>
        new(_ => throw exception);

    public Task<YoutubeVideoFetchResult> FetchAsync(string videoUrl, CancellationToken cancellationToken = default) =>
        Task.FromResult(_respond(videoUrl));
}
