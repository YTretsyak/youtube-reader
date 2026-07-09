namespace Domain;

public sealed class Video
{
    public VideoId Id { get; }
    public VideoUrl Url { get; }
    public VideoStatus Status { get; private set; }
    public string? Title { get; private set; }
    public Summary? Summary { get; private set; }
    public string? Error { get; private set; }

    private Video(VideoId id, VideoUrl url)
    {
        Id = id;
        Url = url;
        Status = VideoStatus.New;
    }

    public static Video Create(VideoId id, VideoUrl url) => new(id, url);

    public void MarkFetchingTranscript()
    {
        EnsureStatus(VideoStatus.New);
        Status = VideoStatus.FetchingTranscript;
    }

    public void MarkSummarizing()
    {
        EnsureStatus(VideoStatus.FetchingTranscript);
        Status = VideoStatus.Summarizing;
    }

    public void MarkProcessed(string title, Summary summary)
    {
        EnsureStatus(VideoStatus.Summarizing);
        Title = title;
        Summary = summary;
        Error = null;
        Status = VideoStatus.Processed;
    }

    public void MarkFailed(string error)
    {
        if (Status != VideoStatus.FetchingTranscript && Status != VideoStatus.Summarizing)
            throw new InvalidOperationException($"Cannot mark failed from {Status}.");

        Error = error;
        Status = VideoStatus.Failed;
    }

    public void Resubmit()
    {
        EnsureStatus(VideoStatus.Failed);
        Error = null;
        Status = VideoStatus.New;
    }

    private void EnsureStatus(VideoStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"Cannot transition from {Status} (expected {expected}).");
    }
}
