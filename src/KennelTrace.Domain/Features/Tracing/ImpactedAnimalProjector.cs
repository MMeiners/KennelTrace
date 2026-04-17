using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedAnimalProjector
{
    public IReadOnlyList<ImpactedAnimalResult> Project(ImpactedAnimalProjectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ImpactedLocations.Count == 0 || request.CandidateMovementStays.Count == 0)
        {
            return [];
        }

        var sourceStayIntervalsById = request.SourceStayIntervals
            .ToDictionary(x => x.StayId);

        var sourceStayIntervalsByLocation = request.SourceStayIntervals
            .GroupBy(x => x.LocationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.StartUtc)
                    .ThenBy(y => y.EndUtc ?? DateTime.MaxValue)
                    .ThenBy(y => y.StayId)
                    .ToArray());

        var candidateStaysByLocation = request.CandidateMovementStays
            .GroupBy(x => x.LocationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.AnimalSortNumber, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(y => y.AnimalSortNumber, StringComparer.Ordinal)
                    .ThenBy(y => y.AnimalSortName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(y => y.AnimalSortName, StringComparer.Ordinal)
                    .ThenBy(y => y.AnimalId)
                    .ThenBy(y => y.StartUtc)
                    .ThenBy(y => y.StayId)
                    .ToArray());

        var results = new Dictionary<(int AnimalId, int ImpactedLocationId), ResultAccumulator>();

        foreach (var impactedLocation in request.ImpactedLocations)
        {
            if (!candidateStaysByLocation.TryGetValue(impactedLocation.LocationId, out var candidateStays))
            {
                continue;
            }

            foreach (var candidateStay in candidateStays)
            {
                foreach (var reason in impactedLocation.Reasons)
                {
                    foreach (var sourceStay in ResolveSourceStays(reason, sourceStayIntervalsById, sourceStayIntervalsByLocation))
                    {
                        if (!TryGetOverlap(
                                sourceStay,
                                candidateStay,
                                request.TraceWindowStartUtc,
                                request.TraceWindowEndUtc,
                                out var overlapStartUtc,
                                out var overlapEndUtc))
                        {
                            continue;
                        }

                        var key = (candidateStay.AnimalId, impactedLocation.LocationId);

                        if (!results.TryGetValue(key, out var accumulator))
                        {
                            accumulator = new ResultAccumulator(candidateStay, impactedLocation.LocationId);
                            results.Add(key, accumulator);
                        }

                        accumulator.AddReason(reason);
                        accumulator.AddOverlap(new ImpactedAnimalStayOverlap(
                            sourceStayId: sourceStay.StayId,
                            sourceLocationId: sourceStay.LocationId,
                            sourceStartUtc: sourceStay.StartUtc,
                            sourceEndUtc: sourceStay.EndUtc,
                            overlappingStayId: candidateStay.StayId,
                            stayLocationId: candidateStay.LocationId,
                            stayStartUtc: candidateStay.StartUtc,
                            stayEndUtc: candidateStay.EndUtc,
                            overlapStartUtc: overlapStartUtc,
                            overlapEndUtc: overlapEndUtc));
                    }
                }
            }
        }

        return results.Values
            .Select(x => x.Build())
            .OrderBy(x => x.AnimalSortNumber, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.AnimalSortNumber, StringComparer.Ordinal)
            .ThenBy(x => x.AnimalSortName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.AnimalSortName, StringComparer.Ordinal)
            .ThenBy(x => x.AnimalId)
            .ThenBy(x => x.ImpactedLocationId)
            .ToArray();
    }

    private static IReadOnlyList<ResolvedTraceSourceStayInterval> ResolveSourceStays(
        ImpactedLocationReason reason,
        IReadOnlyDictionary<long, ResolvedTraceSourceStayInterval> sourceStayIntervalsById,
        IReadOnlyDictionary<int, ResolvedTraceSourceStayInterval[]> sourceStayIntervalsByLocation)
    {
        if (reason.SourceStayId is not null)
        {
            return [sourceStayIntervalsById[reason.SourceStayId.Value]];
        }

        return sourceStayIntervalsByLocation[reason.SourceLocationId];
    }

    private static bool TryGetOverlap(
        ResolvedTraceSourceStayInterval sourceStay,
        TraceCandidateMovementStay candidateStay,
        DateTime traceWindowStartUtc,
        DateTime traceWindowEndUtc,
        out DateTime overlapStartUtc,
        out DateTime overlapEndUtc)
    {
        overlapStartUtc = default;
        overlapEndUtc = default;

        if (!MovementEvent.IntervalsOverlap(sourceStay.StartUtc, sourceStay.EndUtc, traceWindowStartUtc, traceWindowEndUtc))
        {
            return false;
        }

        var sourceWindowStartUtc = sourceStay.StartUtc < traceWindowStartUtc
            ? traceWindowStartUtc
            : sourceStay.StartUtc;

        var sourceWindowEndUtc = MinEndUtc(sourceStay.EndUtc, traceWindowEndUtc);

        if (!MovementEvent.IntervalsOverlap(candidateStay.StartUtc, candidateStay.EndUtc, sourceWindowStartUtc, sourceWindowEndUtc))
        {
            return false;
        }

        overlapStartUtc = sourceWindowStartUtc < candidateStay.StartUtc
            ? candidateStay.StartUtc
            : sourceWindowStartUtc;

        overlapEndUtc = MinEndUtc(candidateStay.EndUtc, sourceWindowEndUtc);
        return overlapStartUtc < overlapEndUtc;
    }

    private static DateTime MinEndUtc(DateTime? endUtc, DateTime otherEndUtc)
    {
        var normalizedEndUtc = endUtc ?? DateTime.MaxValue;
        return normalizedEndUtc <= otherEndUtc
            ? normalizedEndUtc
            : otherEndUtc;
    }

    private sealed class ResultAccumulator
    {
        private readonly int _animalId;
        private readonly AnimalCode _animalNumber;
        private readonly string? _animalName;
        private readonly int _impactedLocationId;
        private readonly Dictionary<ReasonKey, ImpactedLocationReason> _reasons = [];
        private readonly Dictionary<OverlapKey, ImpactedAnimalStayOverlap> _overlaps = [];

        public ResultAccumulator(TraceCandidateMovementStay candidateStay, int impactedLocationId)
        {
            _animalId = candidateStay.AnimalId;
            _animalNumber = candidateStay.AnimalNumber;
            _animalName = candidateStay.AnimalName;
            _impactedLocationId = impactedLocationId;
        }

        public void AddReason(ImpactedLocationReason reason)
        {
            var key = new ReasonKey(
                reason.ReasonCode,
                reason.SourceLocationId,
                reason.SourceStayId,
                reason.MatchKind,
                reason.ScopeLocationId,
                reason.TraversalDepth,
                reason.ViaLinkType);

            _reasons[key] = reason;
        }

        public void AddOverlap(ImpactedAnimalStayOverlap overlap)
        {
            var key = new OverlapKey(overlap.OverlappingStayId, overlap.SourceStayId);
            _overlaps[key] = overlap;
        }

        public ImpactedAnimalResult Build()
        {
            Guard.Against(_reasons.Count == 0, "Impacted animal results must include at least one trace reason.");
            Guard.Against(_overlaps.Count == 0, "Impacted animal results must include at least one overlapping stay.");

            return new ImpactedAnimalResult(
                _animalId,
                _animalNumber,
                _animalName,
                _impactedLocationId,
                _overlaps.Values,
                _reasons.Values);
        }

        private readonly record struct ReasonKey(
            TraceReasonCode ReasonCode,
            int SourceLocationId,
            long? SourceStayId,
            ImpactedLocationMatchKind MatchKind,
            int? ScopeLocationId,
            int TraversalDepth,
            LinkType? ViaLinkType);

        private readonly record struct OverlapKey(
            long OverlappingStayId,
            long SourceStayId);
    }
}
