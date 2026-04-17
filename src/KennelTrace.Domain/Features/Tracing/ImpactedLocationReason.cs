using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedLocationReason
{
    public ImpactedLocationReason(
        TraceReasonCode reasonCode,
        int sourceLocationId,
        long? sourceStayId = null,
        ImpactedLocationMatchKind matchKind = ImpactedLocationMatchKind.ExactLocation,
        int? scopeLocationId = null,
        int traversalDepth = 0,
        LinkType? viaLinkType = null)
    {
        ReasonCode = reasonCode;
        SourceLocationId = Guard.Positive(sourceLocationId, nameof(sourceLocationId));
        SourceStayId = ValidateOptionalPositive(sourceStayId, nameof(sourceStayId));
        MatchKind = matchKind;
        ScopeLocationId = ValidateScopeLocationId(scopeLocationId, matchKind);
        TraversalDepth = Guard.NonNegative(traversalDepth, nameof(traversalDepth));
        ViaLinkType = viaLinkType;

        ValidateReasonMetadata(reasonCode, viaLinkType, TraversalDepth);
    }

    public TraceReasonCode ReasonCode { get; }

    public int SourceLocationId { get; }

    public long? SourceStayId { get; }

    public ImpactedLocationMatchKind MatchKind { get; }

    public int? ScopeLocationId { get; }

    public int TraversalDepth { get; }

    public LinkType? ViaLinkType { get; }

    private static long? ValidateOptionalPositive(long? value, string paramName)
    {
        if (value is null)
        {
            return null;
        }

        Guard.Against(value.Value <= 0, $"{paramName} must be greater than zero.");
        return value.Value;
    }

    private static int? ValidateScopeLocationId(int? scopeLocationId, ImpactedLocationMatchKind matchKind)
    {
        if (matchKind == ImpactedLocationMatchKind.ExactLocation)
        {
            Guard.Against(scopeLocationId is not null, "Exact location matches cannot include a scopeLocationId.");
            return null;
        }

        return Guard.Positive(scopeLocationId ?? 0, nameof(scopeLocationId));
    }

    private static void ValidateReasonMetadata(TraceReasonCode reasonCode, LinkType? viaLinkType, int traversalDepth)
    {
        switch (reasonCode)
        {
            case TraceReasonCode.SameLocation:
            case TraceReasonCode.SameRoom:
                Guard.Against(viaLinkType is not null, $"{reasonCode} reasons cannot include a link type.");
                Guard.Against(traversalDepth != 0, $"{reasonCode} reasons cannot include traversal depth.");
                break;

            case TraceReasonCode.Adjacent:
                Guard.Against(viaLinkType is null || !LinkTypeRules.IsAdjacency(viaLinkType.Value), "Adjacent reasons must reference an adjacency link type.");
                Guard.Against(traversalDepth <= 0, "Adjacent reasons must include positive traversal depth.");
                break;

            case TraceReasonCode.AirflowLinked:
                Guard.Against(viaLinkType != LinkType.Airflow, "AirflowLinked reasons must reference the Airflow link type.");
                Guard.Against(traversalDepth <= 0, "AirflowLinked reasons must include positive traversal depth.");
                break;

            case TraceReasonCode.TransportPathLinked:
                Guard.Against(viaLinkType != LinkType.TransportPath, "TransportPathLinked reasons must reference the TransportPath link type.");
                Guard.Against(traversalDepth <= 0, "TransportPathLinked reasons must include positive traversal depth.");
                break;

            case TraceReasonCode.ConnectedSpace:
                Guard.Against(viaLinkType != LinkType.Connected, "ConnectedSpace reasons must reference the Connected link type.");
                Guard.Against(traversalDepth <= 0, "ConnectedSpace reasons must include positive traversal depth.");
                break;

            default:
                throw new DomainValidationException($"Unsupported trace reason '{reasonCode}'.");
        }
    }
}
