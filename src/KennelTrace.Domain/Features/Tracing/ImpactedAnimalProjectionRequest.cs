using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedAnimalProjectionRequest
{
    public ImpactedAnimalProjectionRequest(
        DateTime traceWindowStartUtc,
        DateTime traceWindowEndUtc,
        IEnumerable<ResolvedTraceSourceStayInterval> sourceStayIntervals,
        IEnumerable<ExpandedImpactedLocation>? impactedLocations = null,
        IEnumerable<TraceCandidateMovementStay>? candidateMovementStays = null)
    {
        TraceWindowStartUtc = Guard.RequiredUtc(traceWindowStartUtc, nameof(traceWindowStartUtc));
        TraceWindowEndUtc = Guard.RequiredUtc(traceWindowEndUtc, nameof(traceWindowEndUtc));
        Guard.Against(TraceWindowEndUtc <= TraceWindowStartUtc, "TraceWindowEndUtc must be greater than TraceWindowStartUtc.");

        SourceStayIntervals = NormalizeSourceStayIntervals(sourceStayIntervals);
        ImpactedLocations = NormalizeImpactedLocations(impactedLocations);
        CandidateMovementStays = NormalizeCandidateMovementStays(candidateMovementStays);

        ValidateReasonSourceMappings(SourceStayIntervals, ImpactedLocations);
    }

    public DateTime TraceWindowStartUtc { get; }

    public DateTime TraceWindowEndUtc { get; }

    public IReadOnlyList<ResolvedTraceSourceStayInterval> SourceStayIntervals { get; }

    public IReadOnlyList<ExpandedImpactedLocation> ImpactedLocations { get; }

    public IReadOnlyList<TraceCandidateMovementStay> CandidateMovementStays { get; }

    private static IReadOnlyList<ResolvedTraceSourceStayInterval> NormalizeSourceStayIntervals(IEnumerable<ResolvedTraceSourceStayInterval> sourceStayIntervals)
    {
        var normalized = sourceStayIntervals?.ToArray() ?? throw new DomainValidationException("sourceStayIntervals is required.");
        Guard.Against(normalized.Length == 0, "sourceStayIntervals must contain at least one value.");
        Guard.Against(
            normalized.GroupBy(x => x.StayId).Any(x => x.Count() > 1),
            "sourceStayIntervals must use unique stay IDs.");

        return normalized
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.EndUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.LocationId)
            .ThenBy(x => x.StayId)
            .ToArray();
    }

    private static IReadOnlyList<ExpandedImpactedLocation> NormalizeImpactedLocations(IEnumerable<ExpandedImpactedLocation>? impactedLocations)
    {
        return (impactedLocations ?? [])
            .GroupBy(x => x.LocationId)
            .Select(x => new ExpandedImpactedLocation(x.Key, x.SelectMany(y => y.Reasons)))
            .OrderBy(x => x.LocationId)
            .ToArray();
    }

    private static IReadOnlyList<TraceCandidateMovementStay> NormalizeCandidateMovementStays(IEnumerable<TraceCandidateMovementStay>? candidateMovementStays)
    {
        var normalized = candidateMovementStays?.ToArray() ?? [];
        Guard.Against(
            normalized.GroupBy(x => x.StayId).Any(x => x.Count() > 1),
            "candidateMovementStays must use unique stay IDs.");

        return normalized
            .OrderBy(x => x.AnimalSortNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.AnimalSortNumber, StringComparer.Ordinal)
            .ThenBy(x => x.AnimalSortName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.AnimalSortName, StringComparer.Ordinal)
            .ThenBy(x => x.AnimalId)
            .ThenBy(x => x.StartUtc)
            .ThenBy(x => x.StayId)
            .ToArray();
    }

    private static void ValidateReasonSourceMappings(
        IReadOnlyList<ResolvedTraceSourceStayInterval> sourceStayIntervals,
        IReadOnlyList<ExpandedImpactedLocation> impactedLocations)
    {
        var sourceStayIds = sourceStayIntervals
            .ToDictionary(x => x.StayId);

        var sourceLocations = sourceStayIntervals
            .Select(x => x.LocationId)
            .ToHashSet();

        foreach (var reason in impactedLocations.SelectMany(x => x.Reasons))
        {
            if (reason.SourceStayId is not null)
            {
                if (!sourceStayIds.TryGetValue(reason.SourceStayId.Value, out var sourceStay))
                {
                    throw new DomainValidationException(
                        $"Impacted location reason source stay '{reason.SourceStayId.Value}' was not provided.");
                }

                Guard.Against(
                    sourceStay.LocationId != reason.SourceLocationId,
                    $"Impacted location reason source stay '{reason.SourceStayId.Value}' does not match source location '{reason.SourceLocationId}'.");

                continue;
            }

            Guard.Against(
                !sourceLocations.Contains(reason.SourceLocationId),
                $"Impacted location reason source location '{reason.SourceLocationId}' was not provided.");
        }
    }
}
