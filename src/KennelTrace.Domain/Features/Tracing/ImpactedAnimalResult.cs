using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedAnimalResult
{
    public ImpactedAnimalResult(
        int animalId,
        int impactedLocationId,
        IEnumerable<long> overlappingStayIds,
        IEnumerable<TraceReasonCode> reasonCodes,
        ImpactedLocationMatchKind matchKind = ImpactedLocationMatchKind.ExactLocation,
        int? scopeLocationId = null,
        int traversalDepth = 0,
        LinkType? viaLinkType = null)
    {
        AnimalId = Guard.Positive(animalId, nameof(animalId));
        ImpactedLocationId = Guard.Positive(impactedLocationId, nameof(impactedLocationId));
        MatchKind = matchKind;
        ScopeLocationId = ValidateScopeLocationId(scopeLocationId, matchKind, impactedLocationId);
        TraversalDepth = Guard.NonNegative(traversalDepth, nameof(traversalDepth));
        ViaLinkType = viaLinkType;
        OverlappingStayIds = NormalizePositiveIds(overlappingStayIds, nameof(overlappingStayIds));
        ReasonCodes = NormalizeReasonCodes(reasonCodes);
    }

    public int AnimalId { get; }

    public int ImpactedLocationId { get; }

    public ImpactedLocationMatchKind MatchKind { get; }

    public int? ScopeLocationId { get; }

    public int TraversalDepth { get; }

    public LinkType? ViaLinkType { get; }

    public IReadOnlyList<long> OverlappingStayIds { get; }

    public IReadOnlyList<TraceReasonCode> ReasonCodes { get; }

    private static int? ValidateScopeLocationId(int? scopeLocationId, ImpactedLocationMatchKind matchKind, int impactedLocationId)
    {
        if (matchKind == ImpactedLocationMatchKind.ExactLocation)
        {
            Guard.Against(scopeLocationId is not null, "Exact location matches cannot include a scopeLocationId.");
            return null;
        }

        var validatedScopeLocationId = Guard.Positive(scopeLocationId ?? 0, nameof(scopeLocationId));
        Guard.Against(validatedScopeLocationId == impactedLocationId, "Scoped location matches must identify a distinct scopeLocationId.");
        return validatedScopeLocationId;
    }

    private static IReadOnlyList<long> NormalizePositiveIds(IEnumerable<long> values, string paramName)
    {
        var normalized = values?.Distinct().ToArray() ?? throw new DomainValidationException($"{paramName} is required.");
        Guard.Against(normalized.Length == 0, $"{paramName} must contain at least one value.");
        Guard.Against(normalized.Any(x => x <= 0), $"{paramName} must contain only positive values.");
        return normalized;
    }

    private static IReadOnlyList<TraceReasonCode> NormalizeReasonCodes(IEnumerable<TraceReasonCode> reasonCodes)
    {
        var normalized = reasonCodes?.Distinct().ToArray() ?? throw new DomainValidationException("reasonCodes is required.");
        Guard.Against(normalized.Length == 0, "Impacted results must include at least one trace reason.");
        return normalized;
    }
}
