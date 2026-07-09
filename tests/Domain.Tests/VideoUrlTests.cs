using Domain;
using Xunit;

namespace Domain.Tests;

public class VideoUrlTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=43s", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?list=PL123&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("http://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?t=5", "dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
    public void TryParse_ValidUrlForms_ExtractsVideoId(string url, string expectedId)
    {
        var result = VideoUrl.TryParse(url, out var videoUrl);

        Assert.True(result);
        Assert.NotNull(videoUrl);
        Assert.Equal(expectedId, videoUrl!.VideoId.Value);
        Assert.Equal(url, videoUrl.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("https://example.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch")]
    [InlineData("https://www.youtube.com/watch?v=")]
    [InlineData("https://www.youtube.com/watch?v=short")]
    [InlineData("https://youtu.be/")]
    [InlineData("https://www.youtube.com/shorts/")]
    public void TryParse_InvalidUrlForms_ReturnsFalse(string? url)
    {
        var result = VideoUrl.TryParse(url, out var videoUrl);

        Assert.False(result);
        Assert.Null(videoUrl);
    }
}
