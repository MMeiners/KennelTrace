using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Locations;

public static class LinkTypeRules
{
    public static bool IsAdjacency(LinkType linkType) =>
        linkType is
            LinkType.AdjacentLeft or
            LinkType.AdjacentRight or
            LinkType.AdjacentAbove or
            LinkType.AdjacentBelow or
            LinkType.AdjacentOther;

    public static bool IsTopology(LinkType linkType) => !IsAdjacency(linkType);

    public static LinkType InverseOf(LinkType linkType) =>
        linkType switch
        {
            LinkType.AdjacentLeft => LinkType.AdjacentRight,
            LinkType.AdjacentRight => LinkType.AdjacentLeft,
            LinkType.AdjacentAbove => LinkType.AdjacentBelow,
            LinkType.AdjacentBelow => LinkType.AdjacentAbove,
            LinkType.AdjacentOther => LinkType.AdjacentOther,
            LinkType.Connected => LinkType.Connected,
            LinkType.Airflow => LinkType.Airflow,
            LinkType.TransportPath => LinkType.TransportPath,
            _ => throw new DomainValidationException($"Unsupported link type '{linkType}'.")
        };
}
