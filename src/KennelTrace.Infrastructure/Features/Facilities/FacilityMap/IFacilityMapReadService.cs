namespace KennelTrace.Infrastructure.Features.Facilities.FacilityMap;

public interface IFacilityMapReadService
{
    Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default);

    Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default);
}
