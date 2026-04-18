using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;

namespace KennelTrace.Infrastructure.Features.Tracing.TracePage;

public sealed record TraceDiseaseProfileOption(
    int DiseaseTraceProfileId,
    int DiseaseId,
    DiseaseCode DiseaseCode,
    string DiseaseName,
    int DefaultLookbackHours);

public sealed record TraceLocationScopeOption(
    int LocationId,
    int FacilityId,
    FacilityCode FacilityCode,
    string FacilityName,
    LocationCode LocationCode,
    string LocationName,
    LocationType LocationType,
    bool IsActive,
    int? RoomLocationId,
    LocationCode? RoomLocationCode,
    string? RoomName,
    LocationType? RoomLocationType);

public sealed record TraceSourceStaySummary(
    AnimalLookupRow Animal,
    AnimalMovementHistoryRow Stay);
