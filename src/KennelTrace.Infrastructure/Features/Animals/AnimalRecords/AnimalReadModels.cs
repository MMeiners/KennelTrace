using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Infrastructure.Features.Animals.AnimalRecords;

public sealed record AnimalLookupRow(
    int AnimalId,
    AnimalCode AnimalNumber,
    string? Name,
    string Species,
    bool IsActive);

public sealed record AnimalCurrentPlacementSummary(
    long MovementEventId,
    DateTime StartUtc,
    int FacilityId,
    FacilityCode FacilityCode,
    string FacilityName,
    int LocationId,
    LocationCode LocationCode,
    string LocationName,
    LocationType LocationType,
    bool LocationIsActive,
    int? RoomLocationId,
    LocationCode? RoomLocationCode,
    string? RoomName,
    LocationType? RoomLocationType);

public sealed record AnimalMovementHistoryRow(
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
    int? RoomLocationId,
    LocationCode? RoomLocationCode,
    string? RoomName,
    LocationType? RoomLocationType);

public sealed record AnimalDetailResult(
    int AnimalId,
    AnimalCode AnimalNumber,
    string? Name,
    string Species,
    string? Sex,
    string? Breed,
    DateOnly? DateOfBirth,
    bool IsActive,
    string? Notes,
    AnimalCurrentPlacementSummary? CurrentPlacement,
    IReadOnlyList<AnimalMovementHistoryRow> MovementHistory);
