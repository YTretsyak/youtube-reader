using Application.Tests.TestDoubles;
using Domain;
using Xunit;

namespace Application.Tests;

public class GetSummaryHistoryTests
{
    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsVideosNewestFirst()
    {
        var repository = new InMemorySummaryRepository();

        var first = Video.Create(new VideoId("aaaaaaaaaaa"), ParseUrl("https://youtu.be/aaaaaaaaaaa"));
        await repository.SaveAsync(first);

        var second = Video.Create(new VideoId("bbbbbbbbbbb"), ParseUrl("https://youtu.be/bbbbbbbbbbb"));
        await repository.SaveAsync(second);

        var third = Video.Create(new VideoId("ccccccccccc"), ParseUrl("https://youtu.be/ccccccccccc"));
        await repository.SaveAsync(third);

        var useCase = new GetSummaryHistory(repository);

        var history = await useCase.ExecuteAsync();

        Assert.Equal(new[] { third, second, first }, history);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatingAnExistingVideo_DoesNotChangeItsPositionInHistory()
    {
        var repository = new InMemorySummaryRepository();

        var first = Video.Create(new VideoId("aaaaaaaaaaa"), ParseUrl("https://youtu.be/aaaaaaaaaaa"));
        await repository.SaveAsync(first);

        var second = Video.Create(new VideoId("bbbbbbbbbbb"), ParseUrl("https://youtu.be/bbbbbbbbbbb"));
        await repository.SaveAsync(second);

        first.MarkFetchingTranscript();
        await repository.SaveAsync(first);

        var useCase = new GetSummaryHistory(repository);
        var history = await useCase.ExecuteAsync();

        Assert.Equal(new[] { second, first }, history);
    }
}
