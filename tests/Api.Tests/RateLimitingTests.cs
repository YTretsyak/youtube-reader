using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests;

public class RateLimitingTests
{
    [Fact]
    public async Task Post_Over30RequestsInWindow_Returns429()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 31; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/summaries", new { url = "https://example.com/not-a-video" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}
