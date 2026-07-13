using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application;
using Domain;
using Xunit;

namespace Api.Tests;

public class SubmitSummaryEndpointTests
{
    [Fact]
    public async Task Post_NewUrl_Returns202AndPublishes()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/summaries", new { url = "https://www.youtube.com/watch?v=dQw4w9WgXcQ" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("fetching-transcript", body.GetProperty("status").GetString());
        Assert.Equal("dQw4w9WgXcQ", body.GetProperty("videoId").GetString());
        Assert.Single(factory.Publisher.PublishedMessages);
    }

    [Fact]
    public async Task Post_AlreadyProcessedUrl_Returns200WithoutPublishing()
    {
        await using var factory = new ApiFactory();
        var url = ParseUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        video.MarkSummarizing();
        video.MarkProcessed("Example video", new Summary("summary text", "github-models", "openai/gpt-4o-mini"));
        await factory.Repository.SaveAsync(video);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/summaries", new { url = url.Value });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("processed", body.GetProperty("status").GetString());
        Assert.Equal("summary text", body.GetProperty("summary").GetString());
        Assert.Empty(factory.Publisher.PublishedMessages);
    }

    [Fact]
    public async Task Post_MissingUrl_Returns400()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/summaries", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(factory.Publisher.PublishedMessages);
    }

    [Fact]
    public async Task Post_NonYoutubeUrl_Returns400()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/summaries", new { url = "https://example.com/not-a-video" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WrongContentType_Returns400()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var content = new StringContent("{\"url\":\"https://www.youtube.com/watch?v=dQw4w9WgXcQ\"}", Encoding.UTF8, "text/plain");

        var response = await client.PostAsync("/api/summaries", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }
}
