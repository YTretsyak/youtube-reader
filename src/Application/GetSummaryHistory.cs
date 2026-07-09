using Domain;

namespace Application;

public sealed class GetSummaryHistory
{
    private readonly ISummaryRepository _repository;

    public GetSummaryHistory(ISummaryRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<Video>> ExecuteAsync(CancellationToken cancellationToken = default)
        => _repository.GetHistoryAsync(cancellationToken);
}
