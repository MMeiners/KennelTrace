using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Locations;

public sealed class LocationLink
{
    private LocationLink(
        Guid id,
        Guid facilityId,
        LinkType linkType,
        Guid fromLocationId,
        Guid toLocationId,
        string? notes)
    {
        Id = id;
        FacilityId = facilityId;
        LinkType = linkType;
        FromLocationId = fromLocationId;
        ToLocationId = toLocationId;
        Notes = notes;
    }

    public Guid Id { get; }

    public Guid FacilityId { get; }

    public LinkType LinkType { get; }

    public Guid FromLocationId { get; }

    public Guid ToLocationId { get; }

    public string? Notes { get; }

    public static LocationLink Create(
        Guid id,
        Location fromLocation,
        Location toLocation,
        LinkType linkType,
        string? notes = null)
    {
        Guard.RequiredId(id, nameof(id));
        Guard.Against(fromLocation.Id == toLocation.Id, "Self-links are invalid.");
        Guard.Against(fromLocation.FacilityId != toLocation.FacilityId, "Cross-facility links are invalid in MVP.");
        Guard.Against(!fromLocation.IsActive || !toLocation.IsActive, "Location links require active endpoints.");

        if (LinkTypeRules.IsAdjacency(linkType))
        {
            Guard.Against(
                fromLocation.LocationType != LocationType.Kennel || toLocation.LocationType != LocationType.Kennel,
                "Adjacency-style links should connect kennels.");
        }
        else
        {
            Guard.Against(
                fromLocation.LocationType == LocationType.Kennel || toLocation.LocationType == LocationType.Kennel,
                "Topology-style links should connect room-like locations unless an intentional admin override is built.");
        }

        return new LocationLink(
            id,
            fromLocation.FacilityId,
            linkType,
            fromLocation.Id,
            toLocation.Id,
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }
}
