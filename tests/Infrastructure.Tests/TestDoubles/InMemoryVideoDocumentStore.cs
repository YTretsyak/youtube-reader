using Infrastructure.Persistence;

namespace Infrastructure.Tests.TestDoubles;

public sealed class InMemoryVideoDocumentStore : IVideoDocumentStore
{
    private readonly Dictionary<string, VideoDocument> _documentsByVideoId = new();
    private readonly List<string> _insertionOrder = new();

    public Task<VideoDocument?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default)
    {
        _documentsByVideoId.TryGetValue(videoId, out var document);
        return Task.FromResult(document);
    }

    public Task UpsertAsync(VideoDocument document, CancellationToken cancellationToken = default)
    {
        if (!_documentsByVideoId.ContainsKey(document.VideoId))
            _insertionOrder.Add(document.VideoId);

        _documentsByVideoId[document.VideoId] = document;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VideoDocument>> FindAllNewestFirstAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VideoDocument> documents = _insertionOrder
            .AsEnumerable()
            .Reverse()
            .Select(id => _documentsByVideoId[id])
            .ToList();

        return Task.FromResult(documents);
    }
}
