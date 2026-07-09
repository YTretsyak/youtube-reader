using Domain;

namespace Application;

public sealed class SummarizerResult
{
    public bool IsSuccess { get; }
    public Summary? Summary { get; }
    public string? Error { get; }

    private SummarizerResult(bool isSuccess, Summary? summary, string? error)
    {
        IsSuccess = isSuccess;
        Summary = summary;
        Error = error;
    }

    public static SummarizerResult Success(Summary summary) => new(true, summary, null);

    public static SummarizerResult Failure(string error) => new(false, null, error);
}
