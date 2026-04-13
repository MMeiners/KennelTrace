using System.Linq.Expressions;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Features.Facilities.FacilityMap;

public sealed class FacilityMapReadService(KennelTraceDbContext dbContext) : IFacilityMapReadService
{
    public async Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Facilities
            .AsNoTracking()
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.FacilityCode)
            .Select(x => new FacilityMapFacilityOption(
                x.FacilityId,
                x.FacilityCode,
                x.Name,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId
                        && (x.LocationType == LocationType.Room
                            || x.LocationType == LocationType.Medical
                            || x.LocationType == LocationType.Isolation
                            || x.LocationType == LocationType.Intake))
            .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.LocationCode)
            .ThenBy(x => x.LocationId)
            .Select(x => new FacilityMapRoomOption(
                x.FacilityId,
                x.LocationId,
                x.LocationCode,
                x.Name,
                x.LocationType,
                x.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default)
    {
        var facility = await dbContext.Facilities
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId)
            .Select(x => new FacilityMapFacilityOption(
                x.FacilityId,
                x.FacilityCode,
                x.Name,
                x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

        if (facility is null)
        {
            return null;
        }

        var room = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId
                        && x.LocationId == roomLocationId
                        && (x.LocationType == LocationType.Room
                            || x.LocationType == LocationType.Medical
                            || x.LocationType == LocationType.Isolation
                            || x.LocationType == LocationType.Intake))
            .Select(LocationRow.Projection)
            .SingleOrDefaultAsync(cancellationToken);

        if (room is null)
        {
            return null;
        }

        var childLocations = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId && x.ParentLocationId == roomLocationId)
            .Select(LocationRow.Projection)
            .ToListAsync(cancellationToken);

        var relevantLocationIds = childLocations.Select(x => x.LocationId)
            .Append(room.LocationId)
            .ToArray();

        var currentOccupancyByLocationId = await dbContext.MovementEvents
            .AsNoTracking()
            .Where(x => x.EndUtc == null && relevantLocationIds.Contains(x.LocationId))
            .GroupBy(x => x.LocationId)
            .Select(x => new { LocationId = x.Key, OccupancyCount = x.Count() })
            .ToDictionaryAsync(x => x.LocationId, x => x.OccupancyCount, cancellationToken);

        var links = await (
                from link in dbContext.LocationLinks.AsNoTracking()
                join fromLocation in dbContext.Locations.AsNoTracking()
                    on new { Id = link.FromLocationId, link.FacilityId } equals new { Id = fromLocation.LocationId, fromLocation.FacilityId }
                join toLocation in dbContext.Locations.AsNoTracking()
                    on new { Id = link.ToLocationId, link.FacilityId } equals new { Id = toLocation.LocationId, toLocation.FacilityId }
                where link.FacilityId == facilityId
                      && link.IsActive
                      && (relevantLocationIds.Contains(link.FromLocationId) || relevantLocationIds.Contains(link.ToLocationId))
                orderby link.LinkType, fromLocation.LocationCode, toLocation.LocationCode, link.LocationLinkId
                select new FacilityMapLocationLink(
                    link.LocationLinkId,
                    link.FromLocationId,
                    fromLocation.LocationCode,
                    fromLocation.Name,
                    link.ToLocationId,
                    toLocation.LocationCode,
                    toLocation.Name,
                    link.LinkType,
                    link.SourceType,
                    link.SourceReference,
                    link.Notes))
            .ToListAsync(cancellationToken);

        var linksByLocationId = relevantLocationIds.ToDictionary(
            locationId => locationId,
            locationId => (IReadOnlyList<FacilityMapLocationLink>)links
                .Where(link => link.FromLocationId == locationId || link.ToLocationId == locationId)
                .ToList());

        var placedLocations = childLocations
            .Where(HasGridPlacement)
            .OrderBy(x => x.GridRow)
            .ThenBy(x => x.GridColumn)
            .ThenBy(x => x.StackLevel)
            .ThenBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.LocationCode)
            .ThenBy(x => x.LocationId)
            .Select(x => BuildLocationDetail(x, currentOccupancyByLocationId, linksByLocationId))
            .ToList();

        var unplacedLocations = childLocations
            .Where(x => !HasGridPlacement(x))
            .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.LocationCode)
            .ThenBy(x => x.LocationId)
            .Select(x => BuildLocationDetail(x, currentOccupancyByLocationId, linksByLocationId))
            .ToList();

        return new RoomMapResult(
            facility,
            BuildLocationDetail(room, currentOccupancyByLocationId, linksByLocationId),
            placedLocations,
            unplacedLocations);
    }

    private static bool HasGridPlacement(LocationRow location) =>
        location.GridRow is not null && location.GridColumn is not null;

    private static FacilityMapLocationDetail BuildLocationDetail(
        LocationRow location,
        IReadOnlyDictionary<int, int> currentOccupancyByLocationId,
        IReadOnlyDictionary<int, IReadOnlyList<FacilityMapLocationLink>> linksByLocationId)
    {
        return new FacilityMapLocationDetail(
            location.LocationId,
            location.FacilityId,
            location.ParentLocationId,
            location.LocationType,
            location.LocationCode,
            location.Name,
            location.IsActive,
            location.GridRow,
            location.GridColumn,
            location.StackLevel,
            location.DisplayOrder,
            location.Notes,
            currentOccupancyByLocationId.GetValueOrDefault(location.LocationId, 0),
            linksByLocationId.GetValueOrDefault(location.LocationId, []));
    }

    private sealed record LocationRow(
        int LocationId,
        int FacilityId,
        int? ParentLocationId,
        LocationType LocationType,
        KennelTrace.Domain.Common.LocationCode LocationCode,
        string Name,
        bool IsActive,
        int? GridRow,
        int? GridColumn,
        int StackLevel,
        int? DisplayOrder,
        string? Notes)
    {
        public static readonly Expression<Func<Location, LocationRow>> Projection = x => new LocationRow(
            x.LocationId,
            x.FacilityId,
            x.ParentLocationId,
            x.LocationType,
            x.LocationCode,
            x.Name,
            x.IsActive,
            x.GridRow,
            x.GridColumn,
            x.StackLevel,
            x.DisplayOrder,
            x.Notes);
    }
}
