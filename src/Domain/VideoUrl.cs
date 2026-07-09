using System.Text.RegularExpressions;

namespace Domain;

public sealed class VideoUrl
{
    private static readonly Regex ValidIdPattern = new("^[A-Za-z0-9_-]{11}$", RegexOptions.Compiled);

    public string Value { get; }
    public VideoId VideoId { get; }

    private VideoUrl(string value, VideoId videoId)
    {
        Value = value;
        VideoId = videoId;
    }

    public static bool TryParse(string? url, out VideoUrl? videoUrl)
    {
        videoUrl = null;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        var id = ExtractVideoId(url);
        if (id is null || !ValidIdPattern.IsMatch(id))
            return false;

        videoUrl = new VideoUrl(url, new VideoId(id));
        return true;
    }

    private static string? ExtractVideoId(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        var host = NormalizeHost(uri.Host);
        var path = uri.AbsolutePath;

        if (host == "youtube.com")
        {
            if (path == "/watch")
                return GetQueryParam(uri.Query, "v");

            if (path.StartsWith("/shorts/", StringComparison.Ordinal))
                return NullIfEmpty(path["/shorts/".Length..]);

            return null;
        }

        if (host == "youtu.be")
            return NullIfEmpty(path.TrimStart('/'));

        return null;
    }

    private static string NormalizeHost(string host)
    {
        host = host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host["www.".Length..];
        if (host.StartsWith("m.", StringComparison.Ordinal)) host = host["m.".Length..];
        return host;
    }

    private static string? GetQueryParam(string query, string name)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && parts[0] == name)
                return NullIfEmpty(Uri.UnescapeDataString(parts[1]));
        }

        return null;
    }

    private static string? NullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;
}
