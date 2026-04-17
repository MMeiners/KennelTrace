using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class TraceGraphLink
{
    public TraceGraphLink(int fromLocationId, int toLocationId, LinkType linkType)
    {
        FromLocationId = Guard.Positive(fromLocationId, nameof(fromLocationId));
        ToLocationId = Guard.Positive(toLocationId, nameof(toLocationId));
        Guard.Against(FromLocationId == ToLocationId, "Trace graph links cannot point to the same location.");

        LinkType = linkType;
    }

    public int FromLocationId { get; }

    public int ToLocationId { get; }

    public LinkType LinkType { get; }
}
