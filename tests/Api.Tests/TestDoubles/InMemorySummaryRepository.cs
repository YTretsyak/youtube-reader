using Application;
using Domain;

namespace Api.Tests.TestDoubles;

public sealed class InMemorySummaryRepository : ISummaryRepository
{
    private readonly Dictionary<string, Video> _videosByVideoId = new();
    private readonly List<string> _insertionOrder = new();

    public Task<Video?> GetByVideoIdAsync(VideoId videoId, CancellationToken cancellationToken = default)
    {
        _videosByVideoId.TryGetValue(videoId.Value, out var video);
        return Task.FromResult(video);
    }

    public Task SaveAsync(Video video, CancellationToken cancellationToken = default)
    {
        if (!_videosByVideoId.ContainsKey(video.Id.Value))
            _insertionOrder.Add(video.Id.Value);

        _videosByVideoId[video.Id.Value] = video;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Video>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Video> history = _insertionOrder
            .AsEnumerable()
            .Reverse()
            .Select(id => _videosByVideoId[id])
            .ToList();

        return Task.FromResult(history);
    }
}
