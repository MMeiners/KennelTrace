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
        return await (
                from location in dbContext.Locations.AsNoTracking()
                join facility in dbContext.Facilities.AsNoTracking()
                    on location.FacilityId equals facility.FacilityId
                where location.IsActive
                orderby facility.Name, facility.FacilityCode, location.Name, location.LocationCode, location.LocationId
                select new TraceLocationScopeOption(
                    location.LocationId,
                    facility.FacilityId,
                    facility.FacilityCode,
                    facility.Name,
                    location.LocationCode,
                    location.Name,
                    location.LocationType))
            .ToListAsync(cancellationToken);
    }
}
