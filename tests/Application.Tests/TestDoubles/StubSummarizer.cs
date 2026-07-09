namespace Application.Tests.TestDoubles;

public sealed class StubSummarizer : ISummarizer
{
    private readonly SummarizerResult _result;

    public StubSummarizer(SummarizerResult result)
    {
        _result = result;
    }

    public Task<SummarizerResult> SummarizeAsync(string transcriptText, CancellationToken cancellationToken = default)
        => Task.FromResult(_result);
}
