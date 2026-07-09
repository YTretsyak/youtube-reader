using Contracts;
using Domain;

namespace Application;

public sealed class SummarizeVideo
{
    private readonly ISummaryRepository _repository;
    private readonly ITranscriptRequestPublisher _publisher;

    public SummarizeVideo(ISummaryRepository repository, ITranscriptRequestPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Video> ExecuteAsync(VideoUrl url, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByVideoIdAsync(url.VideoId, cancellationToken);

        if (existing is null)
        {
            var video = Video.Create(url.VideoId, url);
            await PublishAndMarkFetchingAsync(video, url, cancellationToken);
            return video;
        }

        if (existing.Status is VideoStatus.Processed)
            return existing;

        if (existing.Status is VideoStatus.Failed)
        {
            existing.Resubmit();
            await PublishAndMarkFetchingAsync(existing, url, cancellationToken);
            return existing;
        }

        return existing;
    }

    private async Task PublishAndMarkFetchingAsync(Video video, VideoUrl url, CancellationToken cancellationToken)
    {
        await _repository.SaveAsync(video, cancellationToken);
        await _publisher.PublishAsync(new TranscriptRequested(url.VideoId.Value, url.Value), cancellationToken);
        video.MarkFetchingTranscript();
        await _repository.SaveAsync(video, cancellationToken);
    }
}
