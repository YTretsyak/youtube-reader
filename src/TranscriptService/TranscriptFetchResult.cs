namespace TranscriptService;

public sealed class TranscriptFetchResult
{
    public TranscriptFetchOutcome Outcome { get; }
    public string? Title { get; }
    public string? Transcript { get; }
    public string? Reason { get; }

    private TranscriptFetchResult(TranscriptFetchOutcome outcome, string? title, string? transcript, string? reason)
    {
        Outcome = outcome;
        Title = title;
        Transcript = transcript;
        Reason = reason;
    }

    public static TranscriptFetchResult Success(string title, string transcript) =>
        new(TranscriptFetchOutcome.Success, title, transcript, null);

    public static TranscriptFetchResult Unavailable(string reason) =>
        new(TranscriptFetchOutcome.Unavailable, null, null, reason);

    public static TranscriptFetchResult TransientError(string reason) =>
        new(TranscriptFetchOutcome.TransientError, null, null, reason);
}
