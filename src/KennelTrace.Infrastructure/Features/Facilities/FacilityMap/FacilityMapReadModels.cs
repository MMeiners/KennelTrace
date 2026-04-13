using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Infrastructure.Features.Facilities.FacilityMap;

public sealed record FacilityMapFacilityOption(
    int FacilityId,
    FacilityCode FacilityCode,
    string Name,
    bool IsActive);

public sealed record FacilityMapRoomOption(
    int FacilityId,
    int RoomLocationId,
    LocationCode RoomCode,
    string RoomName,
    LocationType RoomType,
    bool IsActive);

public sealed record FacilityMapLocationLink(
    int LocationLinkId,
    int FromLocationId,
    LocationCode FromLocationCode,
    string FromLocationName,
    int ToLocationId,
    LocationCode ToLocationCode,
    string ToLocationName,
    LinkType LinkType,
    SourceType SourceType,
    string? SourceReference,
    string? Notes);

public sealed record FacilityMapLocationDetail(
    int LocationId,
    int FacilityId,
    int? ParentLocationId,
    LocationType LocationType,
    LocationCode LocationCode,
    string Name,
    bool IsActive,
    int? GridRow,
    int? GridColumn,
    int StackLevel,
    int? DisplayOrder,
    string? Notes,
    int CurrentOccupancyCount,
    IReadOnlyList<FacilityMapLocationLink> Links);

public sealed record RoomMapResult(
    FacilityMapFacilityOption Facility,
    FacilityMapLocationDetail Room,
    IReadOnlyList<FacilityMapLocationDetail> PlacedLocations,
    IReadOnlyList<FacilityMapLocationDetail> UnplacedLocations);
