using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ResolvedTraceSourceLocation
{
    public ResolvedTraceSourceLocation(int locationId)
    {
        LocationId = Guard.Positive(locationId, nameof(locationId));
    }

    public int LocationId { get; }
}
