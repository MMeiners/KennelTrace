using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedAnimalStayOverlap
{
    public ImpactedAnimalStayOverlap(
        long sourceStayId,
        int sourceLocationId,
        DateTime sourceStartUtc,
        DateTime? sourceEndUtc,
        long overlappingStayId,
        int stayLocationId,
        DateTime stayStartUtc,
        DateTime? stayEndUtc,
        DateTime overlapStartUtc,
        DateTime overlapEndUtc)
    {
        Guard.Against(sourceStayId <= 0, "sourceStayId must be greater than zero.");
        Guard.Against(overlappingStayId <= 0, "overlappingStayId must be greater than zero.");

        SourceStayId = sourceStayId;
        SourceLocationId = Guard.Positive(sourceLocationId, nameof(sourceLocationId));
        SourceStartUtc = Guard.RequiredUtc(sourceStartUtc, nameof(sourceStartUtc));
        SourceEndUtc = sourceEndUtc is null ? null : Guard.RequiredUtc(sourceEndUtc.Value, nameof(sourceEndUtc));
        Guard.Against(SourceEndUtc is not null && SourceEndUtc <= SourceStartUtc, "sourceEndUtc must be greater than sourceStartUtc.");

        OverlappingStayId = overlappingStayId;
        StayLocationId = Guard.Positive(stayLocationId, nameof(stayLocationId));
        StayStartUtc = Guard.RequiredUtc(stayStartUtc, nameof(stayStartUtc));
        StayEndUtc = stayEndUtc is null ? null : Guard.RequiredUtc(stayEndUtc.Value, nameof(stayEndUtc));
        Guard.Against(StayEndUtc is not null && StayEndUtc <= StayStartUtc, "stayEndUtc must be greater than stayStartUtc.");

        OverlapStartUtc = Guard.RequiredUtc(overlapStartUtc, nameof(overlapStartUtc));
        OverlapEndUtc = Guard.RequiredUtc(overlapEndUtc, nameof(overlapEndUtc));
        Guard.Against(OverlapEndUtc <= OverlapStartUtc, "overlapEndUtc must be greater than overlapStartUtc.");
    }

    public long SourceStayId { get; }

    public int SourceLocationId { get; }

    public DateTime SourceStartUtc { get; }

    public DateTime? SourceEndUtc { get; }

    public long OverlappingStayId { get; }

    public int StayLocationId { get; }

    public DateTime StayStartUtc { get; }

    public DateTime? StayEndUtc { get; }

    public DateTime OverlapStartUtc { get; }

    public DateTime OverlapEndUtc { get; }
}
