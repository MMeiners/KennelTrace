using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;

namespace KennelTrace.Infrastructure.Features.Tracing.TracePage;

public interface ITracePageReadService
{
    Task<IReadOnlyList<TraceDiseaseProfileOption>> ListActiveDiseaseProfilesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TraceLocationScopeOption>> ListLocationScopeOptionsAsync(CancellationToken cancellationToken = default);

    Task<AnimalLookupRow?> GetSourceAnimalSummaryAsync(int animalId, CancellationToken cancellationToken = default);

    Task<TraceSourceStaySummary?> GetSourceStaySummaryAsync(long movementEventId, CancellationToken cancellationToken = default);

    Task<TraceLocationScopeOption?> GetLocationScopeSummaryAsync(int locationId, CancellationToken cancellationToken = default);
}
