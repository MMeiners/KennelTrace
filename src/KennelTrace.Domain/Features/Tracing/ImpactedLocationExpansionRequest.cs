using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedLocationExpansionRequest
{
    public ImpactedLocationExpansionRequest(
        ImpactedLocationExpansionSettings settings,
        TraceGraphSnapshot snapshot,
        IEnumerable<ResolvedTraceSourceStay>? sourceStays = null,
        IEnumerable<ResolvedTraceSourceLocation>? sourceLocations = null)
    {
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        SourceStays = NormalizeSourceStays(sourceStays);
        SourceLocations = NormalizeSourceLocations(sourceLocations);

        Guard.Against(
            SourceStays.Count == 0 && SourceLocations.Count == 0,
            "At least one source stay or source location is required.");
    }

    public ImpactedLocationExpansionSettings Settings { get; }

    public TraceGraphSnapshot Snapshot { get; }

    public IReadOnlyList<ResolvedTraceSourceStay> SourceStays { get; }

    public IReadOnlyList<ResolvedTraceSourceLocation> SourceLocations { get; }

    private static IReadOnlyList<ResolvedTraceSourceStay> NormalizeSourceStays(IEnumerable<ResolvedTraceSourceStay>? sourceStays)
    {
        return (sourceStays ?? [])
            .GroupBy(x => new { x.StayId, x.LocationId })
            .Select(x => x.First())
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.StayId)
            .ToArray();
    }

    private static IReadOnlyList<ResolvedTraceSourceLocation> NormalizeSourceLocations(IEnumerable<ResolvedTraceSourceLocation>? sourceLocations)
    {
        return (sourceLocations ?? [])
            .GroupBy(x => x.LocationId)
            .Select(x => x.First())
            .OrderBy(x => x.LocationId)
            .ToArray();
    }
}
