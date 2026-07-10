using MongoDB.Bson.Serialization.Attributes;

namespace Infrastructure.Persistence;

[BsonIgnoreExtraElements]
public sealed class VideoDocument
{
    public string VideoId { get; set; } = default!;
    public string VideoUrl { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Title { get; set; }
    public string? SummaryText { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
