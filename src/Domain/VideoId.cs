namespace Domain;

public sealed class VideoId : IEquatable<VideoId>
{
    public string Value { get; }

    public VideoId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Video id cannot be empty.", nameof(value));

        Value = value;
    }

    public override string ToString() => Value;

    public bool Equals(VideoId? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as VideoId);

    public override int GetHashCode() => Value.GetHashCode();
}
