using Application;
using Domain;

namespace Infrastructure.Persistence;

public sealed class MongoSummaryRepository : ISummaryRepository
{
    private readonly IVideoDocumentStore _store;

    public MongoSummaryRepository(IVideoDocumentStore store)
    {
        _store = store;
    }

    public async Task<Video?> GetByVideoIdAsync(VideoId videoId, CancellationToken cancellationToken = default)
    {
        var document = await _store.FindByVideoIdAsync(videoId.Value, cancellationToken);
        return document is null ? null : ToDomain(document);
    }

    public Task SaveAsync(Video video, CancellationToken cancellationToken = default) =>
        _store.UpsertAsync(ToDocument(video), cancellationToken);

    public async Task<IReadOnlyList<Video>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        var documents = await _store.FindAllNewestFirstAsync(cancellationToken);
        return documents.Select(ToDomain).ToList();
    }

    private static VideoDocument ToDocument(Video video) => new()
    {
        VideoId = video.Id.Value,
        VideoUrl = video.Url.Value,
        Status = video.Status.ToString(),
        Title = video.Title,
        SummaryText = video.Summary?.Text,
        Provider = video.Summary?.Provider,
        Model = video.Summary?.Model,
        Error = video.Error
    };

    private static Video ToDomain(VideoDocument document)
    {
        VideoUrl.TryParse(document.VideoUrl, out var url);
        var status = Enum.Parse<VideoStatus>(document.Status);
        var summary = document.SummaryText is null
            ? null
            : new Summary(document.SummaryText, document.Provider!, document.Model!);

        return Video.Restore(new VideoId(document.VideoId), url!, status, document.Title, summary, document.Error);
    }
}
