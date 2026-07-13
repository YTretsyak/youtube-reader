using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Xunit;

namespace Api.Tests;

public class SummaryHistoryEndpointTests
{
    [Fact]
    public async Task GetHistory_ReturnsVideosNewestFirst()
    {
        await using var factory = new ApiFactory();
        var first = Video.Create(new VideoId("aaaaaaaaaaa"), ParseUrl("https://youtu.be/aaaaaaaaaaa"));
        await factory.Repository.SaveAsync(first);
        var second = Video.Create(new VideoId("bbbbbbbbbbb"), ParseUrl("https://youtu.be/bbbbbbbbbbb"));
        await factory.Repository.SaveAsync(second);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/summaries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = body.EnumerateArray().Select(e => e.GetProperty("videoId").GetString()).ToList();
        Assert.Equal(new[] { "bbbbbbbbbbb", "aaaaaaaaaaa" }, ids);
    }

    [Fact]
    public async Task GetHistory_NoVideos_ReturnsEmptyArray()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/summaries");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }
}
