using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedLocationResult
{
    public ImpactedLocationResult(
        int locationId,
        IEnumerable<TraceReasonCode> reasonCodes,
        ImpactedLocationMatchKind matchKind = ImpactedLocationMatchKind.ExactLocation,
        int? scopeLocationId = null,
        int traversalDepth = 0,
        LinkType? viaLinkType = null)
    {
        LocationId = Guard.Positive(locationId, nameof(locationId));
        MatchKind = matchKind;
        ScopeLocationId = ValidateScopeLocationId(scopeLocationId, matchKind, locationId);
        TraversalDepth = Guard.NonNegative(traversalDepth, nameof(traversalDepth));
        ViaLinkType = viaLinkType;
        ReasonCodes = NormalizeReasonCodes(reasonCodes);
    }

    public int LocationId { get; }

    public ImpactedLocationMatchKind MatchKind { get; }

    public int? ScopeLocationId { get; }

    public int TraversalDepth { get; }

    public LinkType? ViaLinkType { get; }

    public IReadOnlyList<TraceReasonCode> ReasonCodes { get; }

    private static int? ValidateScopeLocationId(int? scopeLocationId, ImpactedLocationMatchKind matchKind, int locationId)
    {
        if (matchKind == ImpactedLocationMatchKind.ExactLocation)
        {
            Guard.Against(scopeLocationId is not null, "Exact location matches cannot include a scopeLocationId.");
            return null;
        }

        var validatedScopeLocationId = Guard.Positive(scopeLocationId ?? 0, nameof(scopeLocationId));
        Guard.Against(validatedScopeLocationId == locationId, "Scoped location matches must identify a distinct scopeLocationId.");
        return validatedScopeLocationId;
    }

    private static IReadOnlyList<TraceReasonCode> NormalizeReasonCodes(IEnumerable<TraceReasonCode> reasonCodes)
    {
        var normalized = reasonCodes?.Distinct().ToArray() ?? throw new DomainValidationException("reasonCodes is required.");
        Guard.Against(normalized.Length == 0, "Impacted results must include at least one trace reason.");
        return normalized;
    }
}
