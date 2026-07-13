using Domain;

namespace Api.Contracts;

public sealed record SummaryResponse(
    string Id,
    string VideoUrl,
    string VideoId,
    string Status,
    string? Title,
    string? Summary,
    string? Error,
    DateTimeOffset CreatedAt)
{
    public static SummaryResponse From(Video video) => new(
        Id: video.Id.Value,
        VideoUrl: video.Url.Value,
        VideoId: video.Id.Value,
        Status: ToStatusString(video.Status),
        Title: video.Title,
        Summary: video.Summary?.Text,
        Error: video.Error,
        CreatedAt: video.CreatedAt);

    private static string ToStatusString(VideoStatus status) => status switch
    {
        VideoStatus.New => "new",
        VideoStatus.FetchingTranscript => "fetching-transcript",
        VideoStatus.Summarizing => "summarizing",
        VideoStatus.Processed => "processed",
        VideoStatus.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown video status.")
    };
}
