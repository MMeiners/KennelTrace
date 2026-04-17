using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Infrastructure.Features.Animals.AnimalRecords;

public sealed class AnimalReadService(KennelTraceDbContext dbContext) : IAnimalReadService
{
    public async Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default)
    {
        var normalizedSearchText = NormalizeSearchText(searchText);
        if (normalizedSearchText is null)
        {
            return [];
        }

        var animals = await dbContext.Animals
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return animals
            .Where(x => x.AnimalNumber.Value.Contains(normalizedSearchText, StringComparison.OrdinalIgnoreCase)
                        || (!string.IsNullOrWhiteSpace(x.Name)
                            && x.Name.Contains(normalizedSearchText, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.AnimalNumber.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.AnimalId)
            .Select(x => new AnimalLookupRow(
                x.AnimalId,
                x.AnimalNumber,
                x.Name,
                x.Species,
                x.IsActive))
            .ToList();
    }

    public async Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default)
    {
        var animal = await dbContext.Animals
            .AsNoTracking()
            .Where(x => x.AnimalId == animalId)
            .Select(x => new AnimalDetailRow(
                x.AnimalId,
                x.AnimalNumber,
                x.Name,
                x.Species,
                x.Sex,
                x.Breed,
                x.DateOfBirth,
                x.IsActive,
                x.Notes))
            .SingleOrDefaultAsync(cancellationToken);

        if (animal is null)
        {
            return null;
        }

        var movementRows = await (
                from movement in dbContext.MovementEvents.AsNoTracking()
                join location in dbContext.Locations.AsNoTracking()
                    on movement.LocationId equals location.LocationId
                join facility in dbContext.Facilities.AsNoTracking()
                    on location.FacilityId equals facility.FacilityId
                where movement.AnimalId == animalId
                orderby movement.StartUtc descending, movement.MovementEventId descending
                select new MovementHistoryRowData(
                    movement.MovementEventId,
                    movement.StartUtc,
                    movement.EndUtc,
                    movement.MovementReason,
                    facility.FacilityId,
                    facility.FacilityCode,
                    facility.Name,
                    location.LocationId,
                    location.LocationCode,
                    location.Name,
                    location.LocationType,
                    location.IsActive,
                    location.ParentLocationId))
            .ToListAsync(cancellationToken);

        var parentLocationIds = movementRows
            .Where(x => x.ParentLocationId is not null)
            .Select(x => x.ParentLocationId!.Value)
            .Distinct()
            .ToArray();

        var parentLocationsById = await dbContext.Locations
            .AsNoTracking()
            .Where(x => parentLocationIds.Contains(x.LocationId))
            .Select(x => new ParentLocationRow(
                x.LocationId,
                x.LocationCode,
                x.Name,
                x.LocationType))
            .ToDictionaryAsync(x => x.LocationId, cancellationToken);

        var history = movementRows
            .Select(x => BuildMovementHistoryRow(x, parentLocationsById))
            .ToList();

        var currentPlacement = movementRows
            .Where(x => x.EndUtc is null)
            .Select(x => BuildCurrentPlacementSummary(x, parentLocationsById))
            .FirstOrDefault();

        return new AnimalDetailResult(
            animal.AnimalId,
            animal.AnimalNumber,
            animal.Name,
            animal.Species,
            animal.Sex,
            animal.Breed,
            animal.DateOfBirth,
            animal.IsActive,
            animal.Notes,
            currentPlacement,
            history);
    }

    private static string? NormalizeSearchText(string? searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        return searchText.Trim();
    }

    private static AnimalCurrentPlacementSummary BuildCurrentPlacementSummary(
        MovementHistoryRowData movement,
        IReadOnlyDictionary<int, ParentLocationRow> parentLocationsById)
    {
        var room = ResolveRoomLocation(movement, parentLocationsById);

        return new AnimalCurrentPlacementSummary(
            movement.MovementEventId,
            movement.StartUtc,
            movement.FacilityId,
            movement.FacilityCode,
            movement.FacilityName,
            movement.LocationId,
            movement.LocationCode,
            movement.LocationName,
            movement.LocationType,
            movement.LocationIsActive,
            room?.LocationId,
            room?.LocationCode,
            room?.Name,
            room?.LocationType);
    }

    private static AnimalMovementHistoryRow BuildMovementHistoryRow(
        MovementHistoryRowData movement,
        IReadOnlyDictionary<int, ParentLocationRow> parentLocationsById)
    {
        var room = ResolveRoomLocation(movement, parentLocationsById);

        return new AnimalMovementHistoryRow(
            movement.MovementEventId,
            movement.StartUtc,
            movement.EndUtc,
            movement.MovementReason,
            movement.FacilityId,
            movement.FacilityCode,
            movement.FacilityName,
            movement.LocationId,
            movement.LocationCode,
            movement.LocationName,
            movement.LocationType,
            movement.LocationIsActive,
            room?.LocationId,
            room?.LocationCode,
            room?.Name,
            room?.LocationType);
    }

    private static ParentLocationRow? ResolveRoomLocation(
        MovementHistoryRowData movement,
        IReadOnlyDictionary<int, ParentLocationRow> parentLocationsById)
    {
        if (LocationTypeRules.IsRoomLike(movement.LocationType))
        {
            return new ParentLocationRow(
                movement.LocationId,
                movement.LocationCode,
                movement.LocationName,
                movement.LocationType);
        }

        return movement.ParentLocationId is not null
            ? parentLocationsById.GetValueOrDefault(movement.ParentLocationId.Value)
            : null;
    }

    private sealed record AnimalDetailRow(
        int AnimalId,
        AnimalCode AnimalNumber,
        string? Name,
        string Species,
        string? Sex,
        string? Breed,
        DateOnly? DateOfBirth,
        bool IsActive,
        string? Notes);

    private sealed record MovementHistoryRowData(
        long MovementEventId,
        DateTime StartUtc,
        DateTime? EndUtc,
        string? MovementReason,
        int FacilityId,
        FacilityCode FacilityCode,
        string FacilityName,
        int LocationId,
        LocationCode LocationCode,
        string LocationName,
        LocationType LocationType,
        bool LocationIsActive,
        int? ParentLocationId);

    private sealed record ParentLocationRow(
        int LocationId,
        LocationCode LocationCode,
        string Name,
        LocationType LocationType);
}
