using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ResolvedTraceSourceStay
{
    public ResolvedTraceSourceStay(long stayId, int locationId)
    {
        Guard.Against(stayId <= 0, "stayId must be greater than zero.");

        StayId = stayId;
        LocationId = Guard.Positive(locationId, nameof(locationId));
    }

    public long StayId { get; }

    public int LocationId { get; }
}
