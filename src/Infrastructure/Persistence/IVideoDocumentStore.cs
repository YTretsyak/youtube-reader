namespace Infrastructure.Persistence;

public interface IVideoDocumentStore
{
    Task<VideoDocument?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default);

    Task UpsertAsync(VideoDocument document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VideoDocument>> FindAllNewestFirstAsync(CancellationToken cancellationToken = default);
}
