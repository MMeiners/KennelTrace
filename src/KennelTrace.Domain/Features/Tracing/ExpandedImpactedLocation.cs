using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ExpandedImpactedLocation
{
    public ExpandedImpactedLocation(int locationId, IEnumerable<ImpactedLocationReason> reasons)
    {
        LocationId = Guard.Positive(locationId, nameof(locationId));
        Reasons = NormalizeReasons(reasons);
        ReasonCodes = Reasons
            .Select(x => x.ReasonCode)
            .Distinct()
            .ToArray();
    }

    public int LocationId { get; }

    public IReadOnlyList<TraceReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ImpactedLocationReason> Reasons { get; }

    private static IReadOnlyList<ImpactedLocationReason> NormalizeReasons(IEnumerable<ImpactedLocationReason> reasons)
    {
        var normalized = reasons?.ToArray() ?? throw new DomainValidationException("reasons is required.");
        Guard.Against(normalized.Length == 0, "Expanded impacted locations must include at least one reason.");

        return normalized
            .GroupBy(x => new
            {
                x.ReasonCode,
                x.SourceLocationId,
                x.SourceStayId,
                x.MatchKind,
                x.ScopeLocationId,
                x.ViaLinkType,
                x.TraversalDepth
            })
            .Select(x => x.First())
            .OrderBy(x => x.ReasonCode)
            .ThenBy(x => x.MatchKind)
            .ThenBy(x => x.ScopeLocationId)
            .ThenBy(x => x.TraversalDepth)
            .ThenBy(x => x.ViaLinkType)
            .ThenBy(x => x.SourceLocationId)
            .ThenBy(x => x.SourceStayId)
            .ToArray();
    }
}
