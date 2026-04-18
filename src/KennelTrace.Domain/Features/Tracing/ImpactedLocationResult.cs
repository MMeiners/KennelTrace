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
        LinkType? viaLinkType = null,
        IEnumerable<ImpactedLocationReason>? reasons = null)
    {
        LocationId = Guard.Positive(locationId, nameof(locationId));
        ReasonCodes = NormalizeReasonCodes(reasonCodes);
        Reasons = NormalizeReasons(reasons);

        if (Reasons.Count > 0)
        {
            var primaryReason = Reasons[0];
            MatchKind = primaryReason.MatchKind;
            ScopeLocationId = ValidateScopeLocationId(primaryReason.ScopeLocationId, primaryReason.MatchKind, locationId);
            TraversalDepth = Guard.NonNegative(primaryReason.TraversalDepth, nameof(traversalDepth));
            ViaLinkType = primaryReason.ViaLinkType;
            ValidateReasonCoverage(ReasonCodes, Reasons);
            return;
        }

        MatchKind = matchKind;
        ScopeLocationId = ValidateScopeLocationId(scopeLocationId, matchKind, locationId);
        TraversalDepth = Guard.NonNegative(traversalDepth, nameof(traversalDepth));
        ViaLinkType = viaLinkType;
    }

    public int LocationId { get; }

    public ImpactedLocationMatchKind MatchKind { get; }

    public int? ScopeLocationId { get; }

    public int TraversalDepth { get; }

    public LinkType? ViaLinkType { get; }

    public IReadOnlyList<TraceReasonCode> ReasonCodes { get; }

    public IReadOnlyList<ImpactedLocationReason> Reasons { get; }

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

    private static IReadOnlyList<ImpactedLocationReason> NormalizeReasons(IEnumerable<ImpactedLocationReason>? reasons)
    {
        var normalized = reasons?.ToArray() ?? [];

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

    private static void ValidateReasonCoverage(
        IReadOnlyCollection<TraceReasonCode> reasonCodes,
        IReadOnlyCollection<ImpactedLocationReason> reasons)
    {
        var reasonCodeSet = reasonCodes.ToHashSet();
        var metadataReasonCodeSet = reasons.Select(x => x.ReasonCode).ToHashSet();

        Guard.Against(
            !reasonCodeSet.SetEquals(metadataReasonCodeSet),
            "reasonCodes must match the supplied impacted-location reason metadata.");
    }
}
