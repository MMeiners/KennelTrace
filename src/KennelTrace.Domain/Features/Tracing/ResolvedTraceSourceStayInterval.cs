using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ResolvedTraceSourceStayInterval
{
    public ResolvedTraceSourceStayInterval(
        long stayId,
        int locationId,
        DateTime startUtc,
        DateTime? endUtc = null)
    {
        Guard.Against(stayId <= 0, "stayId must be greater than zero.");

        StayId = stayId;
        LocationId = Guard.Positive(locationId, nameof(locationId));
        StartUtc = Guard.RequiredUtc(startUtc, nameof(startUtc));
        EndUtc = endUtc is null ? null : Guard.RequiredUtc(endUtc.Value, nameof(endUtc));
        Guard.Against(EndUtc is not null && EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
    }

    public long StayId { get; }

    public int LocationId { get; }

    public DateTime StartUtc { get; }

    public DateTime? EndUtc { get; }
}
