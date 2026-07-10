namespace TranscriptService;

public sealed class YoutubeVideoFetchResult
{
    public string Title { get; }
    public string? TranscriptText { get; }

    public YoutubeVideoFetchResult(string title, string? transcriptText)
    {
        Title = title;
        TranscriptText = transcriptText;
    }
}
