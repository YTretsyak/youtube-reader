namespace Application;

public interface ISummarizer
{
    Task<SummarizerResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default);
}
