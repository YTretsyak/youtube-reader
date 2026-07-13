using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Domain;
using Xunit;

namespace Api.Tests;

public class GetSummaryByIdEndpointTests
{
    [Fact]
    public async Task GetById_ExistingVideo_ReturnsRecordWithStatus()
    {
        await using var factory = new ApiFactory();
        var url = ParseUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        await factory.Repository.SaveAsync(video);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/summaries/dQw4w9WgXcQ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dQw4w9WgXcQ", body.GetProperty("id").GetString());
        Assert.Equal("fetching-transcript", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/summaries/unknownvid");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }
}
