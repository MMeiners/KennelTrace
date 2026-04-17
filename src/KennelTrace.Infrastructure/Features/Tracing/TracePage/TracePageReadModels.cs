using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

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
    LocationType LocationType);
