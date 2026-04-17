using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class TraceGraphSnapshot
{
    public TraceGraphSnapshot(
        IEnumerable<TraceGraphLocation>? locations = null,
        IEnumerable<TraceGraphLink>? links = null)
    {
        Locations = NormalizeLocations(locations);
        Links = NormalizeLinks(links);
    }

    public IReadOnlyList<TraceGraphLocation> Locations { get; }

    public IReadOnlyList<TraceGraphLink> Links { get; }

    private static IReadOnlyList<TraceGraphLocation> NormalizeLocations(IEnumerable<TraceGraphLocation>? locations)
    {
        var normalized = locations?.ToArray() ?? [];
        Guard.Against(normalized.Any(x => x is null), "Trace graph locations cannot contain null entries.");
        Guard.Against(
            normalized.GroupBy(x => x.LocationId).Any(x => x.Count() > 1),
            "Trace graph locations must use unique location IDs.");

        return normalized
            .OrderBy(x => x.LocationId)
            .ToArray();
    }

    private static IReadOnlyList<TraceGraphLink> NormalizeLinks(IEnumerable<TraceGraphLink>? links)
    {
        var normalized = links?.ToArray() ?? [];
        Guard.Against(normalized.Any(x => x is null), "Trace graph links cannot contain null entries.");

        return normalized
            .GroupBy(x => new { x.FromLocationId, x.ToLocationId, x.LinkType })
            .Select(x => x.First())
            .OrderBy(x => x.FromLocationId)
            .ThenBy(x => x.ToLocationId)
            .ThenBy(x => x.LinkType)
            .ToArray();
    }
}
