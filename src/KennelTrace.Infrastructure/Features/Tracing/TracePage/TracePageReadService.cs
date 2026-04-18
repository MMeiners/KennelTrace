using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Features.Tracing.TracePage;

public sealed class TracePageReadService(KennelTraceDbContext dbContext) : ITracePageReadService
{
    public async Task<IReadOnlyList<TraceDiseaseProfileOption>> ListActiveDiseaseProfilesAsync(CancellationToken cancellationToken = default)
    {
        return await (
                from profile in dbContext.DiseaseTraceProfiles.AsNoTracking()
                join disease in dbContext.Diseases.AsNoTracking()
                    on profile.DiseaseId equals disease.DiseaseId
                where profile.IsActive && disease.IsActive
                orderby disease.Name, disease.DiseaseCode, profile.DiseaseTraceProfileId
                select new TraceDiseaseProfileOption(
                    profile.DiseaseTraceProfileId,
                    disease.DiseaseId,
                    disease.DiseaseCode,
                    disease.Name,
                    profile.DefaultLookbackHours))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TraceLocationScopeOption>> ListLocationScopeOptionsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await QueryLocationScopeRows(activeOnly: true, locationId: null, cancellationToken);
        return rows.Select(BuildLocationScopeOption).ToList();
    }

    public Task<AnimalLookupRow?> GetSourceAnimalSummaryAsync(int animalId, CancellationToken cancellationToken = default)
    {
        return dbContext.Animals
            .AsNoTracking()
            .Where(x => x.AnimalId == animalId)
            .Select(x => new AnimalLookupRow(
                x.AnimalId,
                x.AnimalNumber,
                x.Name,
                x.Species,
                x.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<TraceSourceStaySummary?> GetSourceStaySummaryAsync(long movementEventId, CancellationToken cancellationToken = default)
    {
        var row = await (
                from movement in dbContext.MovementEvents.AsNoTracking()
                join animal in dbContext.Animals.AsNoTracking()
                    on movement.AnimalId equals animal.AnimalId
                join location in dbContext.Locations.AsNoTracking()
                    on movement.LocationId equals location.LocationId
                join facility in dbContext.Facilities.AsNoTracking()
                    on location.FacilityId equals facility.FacilityId
                join parent in dbContext.Locations.AsNoTracking()
                    on location.ParentLocationId equals parent.LocationId into parentJoin
                from parent in parentJoin.DefaultIfEmpty()
                where movement.MovementEventId == movementEventId
                select new SourceStayRow(
                    movement.MovementEventId,
                    movement.StartUtc,
                    movement.EndUtc,
                    movement.MovementReason,
                    animal.AnimalId,
                    animal.AnimalNumber,
                    animal.Name,
                    animal.Species,
                    animal.IsActive,
                    facility.FacilityId,
                    facility.FacilityCode,
                    facility.Name,
                    location.LocationId,
                    location.LocationCode,
                    location.Name,
                    location.LocationType,
                    location.IsActive,
                    parent == null ? null : parent.LocationId,
                    parent == null ? null : parent.LocationCode,
                    parent == null ? null : parent.Name,
                    parent == null ? null : parent.LocationType))
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        return new TraceSourceStaySummary(
            new AnimalLookupRow(
                row.AnimalId,
                row.AnimalNumber,
                row.AnimalName,
                row.Species,
                row.AnimalIsActive),
            new AnimalMovementHistoryRow(
                row.MovementEventId,
                row.StartUtc,
                row.EndUtc,
                row.MovementReason,
                row.FacilityId,
                row.FacilityCode,
                row.FacilityName,
                row.LocationId,
                row.LocationCode,
                row.LocationName,
                row.LocationType,
                row.LocationIsActive,
                row.RoomLocationId,
                row.RoomLocationCode,
                row.RoomName,
                row.RoomLocationType));
    }

    public async Task<TraceLocationScopeOption?> GetLocationScopeSummaryAsync(int locationId, CancellationToken cancellationToken = default)
    {
        var rows = await QueryLocationScopeRows(activeOnly: false, locationId, cancellationToken);
        return rows.Select(BuildLocationScopeOption).SingleOrDefault();
    }

    private async Task<List<LocationScopeRow>> QueryLocationScopeRows(
        bool activeOnly,
        int? locationId,
        CancellationToken cancellationToken)
    {
        var query =
            from location in dbContext.Locations.AsNoTracking()
            join facility in dbContext.Facilities.AsNoTracking()
                on location.FacilityId equals facility.FacilityId
            join parent in dbContext.Locations.AsNoTracking()
                on location.ParentLocationId equals parent.LocationId into parentJoin
            from parent in parentJoin.DefaultIfEmpty()
            select new { location, facility, parent };

        if (activeOnly)
        {
            query = query.Where(x => x.location.IsActive);
        }

        if (locationId.HasValue)
        {
            query = query.Where(x => x.location.LocationId == locationId.Value);
        }

        return await query
            .OrderBy(x => x.facility.Name)
            .ThenBy(x => x.facility.FacilityCode)
            .ThenBy(x => x.location.Name)
            .ThenBy(x => x.location.LocationCode)
            .ThenBy(x => x.location.LocationId)
            .Select(x => new LocationScopeRow(
                x.location.LocationId,
                x.facility.FacilityId,
                x.facility.FacilityCode,
                x.facility.Name,
                x.location.LocationCode,
                x.location.Name,
                x.location.LocationType,
                x.location.IsActive,
                x.parent == null ? null : x.parent.LocationId,
                x.parent == null ? null : x.parent.LocationCode,
                x.parent == null ? null : x.parent.Name,
                x.parent == null ? null : x.parent.LocationType))
            .ToListAsync(cancellationToken);
    }

    private static TraceLocationScopeOption BuildLocationScopeOption(LocationScopeRow row)
    {
        var room = ResolveRoomContext(row);

        return new TraceLocationScopeOption(
            row.LocationId,
            row.FacilityId,
            row.FacilityCode,
            row.FacilityName,
            row.LocationCode,
            row.LocationName,
            row.LocationType,
            row.IsActive,
            room?.LocationId,
            room?.LocationCode,
            room?.LocationName,
            room?.LocationType);
    }

    private static RoomContext? ResolveRoomContext(LocationScopeRow row)
    {
        if (LocationTypeRules.IsRoomLike(row.LocationType))
        {
            return new RoomContext(
                row.LocationId,
                row.LocationCode,
                row.LocationName,
                row.LocationType);
        }

        if (row.ParentLocationId.HasValue
            && row.ParentLocationCode is not null
            && row.ParentLocationType.HasValue
            && !string.IsNullOrWhiteSpace(row.ParentLocationName)
            && LocationTypeRules.IsRoomLike(row.ParentLocationType.Value))
        {
            var parentLocationCode = row.ParentLocationCode.Value;

            return new RoomContext(
                row.ParentLocationId.Value,
                parentLocationCode,
                row.ParentLocationName!,
                row.ParentLocationType.Value);
        }

        return null;
    }

    private sealed record LocationScopeRow(
        int LocationId,
        int FacilityId,
        KennelTrace.Domain.Common.FacilityCode FacilityCode,
        string FacilityName,
        KennelTrace.Domain.Common.LocationCode LocationCode,
        string LocationName,
        LocationType LocationType,
        bool IsActive,
        int? ParentLocationId,
        KennelTrace.Domain.Common.LocationCode? ParentLocationCode,
        string? ParentLocationName,
        LocationType? ParentLocationType);

    private sealed record RoomContext(
        int LocationId,
        KennelTrace.Domain.Common.LocationCode LocationCode,
        string LocationName,
        LocationType LocationType);

    private sealed record SourceStayRow(
        long MovementEventId,
        DateTime StartUtc,
        DateTime? EndUtc,
        string? MovementReason,
        int AnimalId,
        KennelTrace.Domain.Common.AnimalCode AnimalNumber,
        string? AnimalName,
        string Species,
        bool AnimalIsActive,
        int FacilityId,
        KennelTrace.Domain.Common.FacilityCode FacilityCode,
        string FacilityName,
        int LocationId,
        KennelTrace.Domain.Common.LocationCode LocationCode,
        string LocationName,
        LocationType LocationType,
        bool LocationIsActive,
        int? RoomLocationId,
        KennelTrace.Domain.Common.LocationCode? RoomLocationCode,
        string? RoomName,
        LocationType? RoomLocationType);
}
