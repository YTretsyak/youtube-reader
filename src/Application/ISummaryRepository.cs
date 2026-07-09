using Domain;

namespace Application;

public interface ISummaryRepository
{
    Task<Video?> GetByVideoIdAsync(VideoId videoId, CancellationToken cancellationToken = default);

    Task SaveAsync(Video video, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Video>> GetHistoryAsync(CancellationToken cancellationToken = default);
}
