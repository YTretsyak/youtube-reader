using MongoDB.Driver;

namespace Infrastructure.Persistence;

public sealed class MongoVideoDocumentStore : IVideoDocumentStore
{
    private readonly IMongoCollection<VideoDocument> _collection;

    public MongoVideoDocumentStore(IMongoCollection<VideoDocument> collection)
    {
        _collection = collection;
    }

    public static Task EnsureIndexesAsync(IMongoCollection<VideoDocument> collection, CancellationToken cancellationToken = default)
    {
        var indexModel = new CreateIndexModel<VideoDocument>(
            Builders<VideoDocument>.IndexKeys.Ascending(d => d.VideoId),
            new CreateIndexOptions { Unique = true });

        return collection.Indexes.CreateOneAsync(indexModel, cancellationToken: cancellationToken);
    }

    public async Task<VideoDocument?> FindByVideoIdAsync(string videoId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoDocument>.Filter.Eq(d => d.VideoId, videoId);
        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task UpsertAsync(VideoDocument document, CancellationToken cancellationToken = default)
    {
        var filter = Builders<VideoDocument>.Filter.Eq(d => d.VideoId, document.VideoId);
        var now = DateTime.UtcNow;
        var update = Builders<VideoDocument>.Update
            .Set(d => d.VideoUrl, document.VideoUrl)
            .Set(d => d.Status, document.Status)
            .Set(d => d.Title, document.Title)
            .Set(d => d.SummaryText, document.SummaryText)
            .Set(d => d.Provider, document.Provider)
            .Set(d => d.Model, document.Model)
            .Set(d => d.Error, document.Error)
            .Set(d => d.UpdatedAt, now)
            .SetOnInsert(d => d.CreatedAt, now);

        await _collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<IReadOnlyList<VideoDocument>> FindAllNewestFirstAsync(CancellationToken cancellationToken = default)
    {
        return await _collection.Find(FilterDefinition<VideoDocument>.Empty)
            .SortByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
