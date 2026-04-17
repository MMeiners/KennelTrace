namespace KennelTrace.Infrastructure.Features.Tracing.TracePage;

public interface ITracePageReadService
{
    Task<IReadOnlyList<TraceDiseaseProfileOption>> ListActiveDiseaseProfilesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraceLocationScopeOption>> ListLocationScopeOptionsAsync(CancellationToken cancellationToken = default);
}
