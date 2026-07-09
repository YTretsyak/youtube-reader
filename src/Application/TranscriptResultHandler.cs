using Contracts;
using Domain;

namespace Application;

public sealed class TranscriptResultHandler
{
    private readonly ISummaryRepository _repository;
    private readonly ISummarizer _summarizer;

    public TranscriptResultHandler(ISummaryRepository repository, ISummarizer summarizer)
    {
        _repository = repository;
        _summarizer = summarizer;
    }

    public async Task HandleReadyAsync(TranscriptReady message, CancellationToken cancellationToken = default)
    {
        var video = await _repository.GetByVideoIdAsync(new VideoId(message.VideoId), cancellationToken);
        if (video is null || IsAlreadyFinal(video))
            return;

        video.MarkSummarizing();
        await _repository.SaveAsync(video, cancellationToken);

        var result = await _summarizer.SummarizeAsync(message.Transcript, cancellationToken);

        if (result.IsSuccess)
            video.MarkProcessed(message.Title, result.Summary!);
        else
            video.MarkFailed(result.Error!);

        await _repository.SaveAsync(video, cancellationToken);
    }

    public async Task HandleUnavailableAsync(TranscriptUnavailable message, CancellationToken cancellationToken = default)
    {
        var video = await _repository.GetByVideoIdAsync(new VideoId(message.VideoId), cancellationToken);
        if (video is null || IsAlreadyFinal(video))
            return;

        video.MarkFailed(message.Reason);
        await _repository.SaveAsync(video, cancellationToken);
    }

    private static bool IsAlreadyFinal(Video video) =>
        video.Status is VideoStatus.Processed or VideoStatus.Failed;
}
