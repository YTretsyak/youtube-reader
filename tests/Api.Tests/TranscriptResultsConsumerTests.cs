using System.Text;
using System.Text.Json;
using Api.Messaging;
using Api.Tests.TestDoubles;
using Application;
using Contracts;
using Domain;
using Infrastructure.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client.Events;
using Xunit;

namespace Api.Tests;

public class TranscriptResultsConsumerTests
{
    private static BasicDeliverEventArgs BuildDelivery(string routingKey, object message) => new(
        consumerTag: "test",
        deliveryTag: 1,
        redelivered: false,
        exchange: RabbitMqTopology.ResultsExchangeName,
        routingKey: routingKey,
        properties: new RabbitMQ.Client.BasicProperties(),
        body: JsonSerializer.SerializeToUtf8Bytes(message),
        cancellationToken: CancellationToken.None);

    private static VideoUrl ParseUrl(string url)
    {
        VideoUrl.TryParse(url, out var videoUrl);
        return videoUrl!;
    }

    [Fact]
    public async Task HandleDeliveryAsync_ReadyRoutingKey_CallsHandlerAndReturnsSuccess()
    {
        var repository = new InMemorySummaryRepository();
        var url = ParseUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        await repository.SaveAsync(video);

        var summarizer = new StubSummarizer(SummarizerResult.Success(new Summary("summary text", "github-models", "openai/gpt-4o-mini")));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var consumer = new TranscriptResultsConsumer(NullLogger<TranscriptResultsConsumer>.Instance, null!, handler, new ManualAckPolicy(5));
        var delivery = BuildDelivery(RabbitMqTopology.TranscriptReadyRoutingKey, new TranscriptReady(video.Id.Value, "Example video", "full transcript text"));

        var outcome = await consumer.HandleDeliveryAsync(delivery, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Success, outcome);
        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Processed, updated!.Status);
    }

    [Fact]
    public async Task HandleDeliveryAsync_UnavailableRoutingKey_CallsHandlerAndReturnsSuccess()
    {
        var repository = new InMemorySummaryRepository();
        var url = ParseUrl("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        var video = Video.Create(url.VideoId, url);
        video.MarkFetchingTranscript();
        await repository.SaveAsync(video);

        var summarizer = new StubSummarizer(SummarizerResult.Failure("should not be called"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var consumer = new TranscriptResultsConsumer(NullLogger<TranscriptResultsConsumer>.Instance, null!, handler, new ManualAckPolicy(5));
        var delivery = BuildDelivery(RabbitMqTopology.TranscriptUnavailableRoutingKey, new TranscriptUnavailable(video.Id.Value, "no captions available"));

        var outcome = await consumer.HandleDeliveryAsync(delivery, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.Success, outcome);
        var updated = await repository.GetByVideoIdAsync(video.Id);
        Assert.Equal(VideoStatus.Failed, updated!.Status);
        Assert.Equal("no captions available", updated.Error);
    }

    [Fact]
    public async Task HandleDeliveryAsync_UnknownRoutingKey_ReturnsPoisonFailure()
    {
        var repository = new InMemorySummaryRepository();
        var summarizer = new StubSummarizer(SummarizerResult.Failure("should not be called"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var consumer = new TranscriptResultsConsumer(NullLogger<TranscriptResultsConsumer>.Instance, null!, handler, new ManualAckPolicy(5));
        var delivery = BuildDelivery("unknown.routing.key", new { });

        var outcome = await consumer.HandleDeliveryAsync(delivery, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PoisonFailure, outcome);
    }

    [Fact]
    public async Task HandleDeliveryAsync_UndeserializableBody_ReturnsPoisonFailure()
    {
        var repository = new InMemorySummaryRepository();
        var summarizer = new StubSummarizer(SummarizerResult.Failure("should not be called"));
        var handler = new TranscriptResultHandler(repository, summarizer);
        var consumer = new TranscriptResultsConsumer(NullLogger<TranscriptResultsConsumer>.Instance, null!, handler, new ManualAckPolicy(5));
        var delivery = new BasicDeliverEventArgs(
            consumerTag: "test", deliveryTag: 1, redelivered: false,
            exchange: RabbitMqTopology.ResultsExchangeName, routingKey: RabbitMqTopology.TranscriptReadyRoutingKey,
            properties: new RabbitMQ.Client.BasicProperties(), body: Encoding.UTF8.GetBytes("not valid json"),
            cancellationToken: CancellationToken.None);

        var outcome = await consumer.HandleDeliveryAsync(delivery, CancellationToken.None);

        Assert.Equal(DeliveryOutcome.PoisonFailure, outcome);
    }
}
