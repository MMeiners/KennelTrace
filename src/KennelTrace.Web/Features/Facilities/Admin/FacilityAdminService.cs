using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Facilities.Admin;

public interface IFacilityAdminService
{
    Task<IReadOnlyList<FacilityAdminListItem>> ListFacilitiesAsync(CancellationToken cancellationToken = default);

    Task<FacilitySaveResult> SaveAsync(FacilitySaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class FacilityAdminService(
    KennelTraceDbContext dbContext,
    IAuthorizationService authorizationService) : IFacilityAdminService
{
    public async Task<IReadOnlyList<FacilityAdminListItem>> ListFacilitiesAsync(CancellationToken cancellationToken = default)
    {
        var facilities = await dbContext.Facilities
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return facilities
            .OrderBy(facility => facility.FacilityCode.Value)
            .Select(facility => new FacilityAdminListItem(
                facility.FacilityId,
                facility.FacilityCode.Value,
                facility.Name,
                facility.TimeZoneId,
                facility.IsActive,
                facility.Notes,
                facility.CreatedUtc,
                facility.ModifiedUtc))
            .ToList();
    }

    public async Task<FacilitySaveResult> SaveAsync(FacilitySaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return FacilitySaveResult.Forbidden();
        }

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return FacilitySaveResult.ValidationFailed(validationErrors);
        }

        var facilityCode = new FacilityCode(request.FacilityCode.Trim());
        var duplicateCodeExists = await dbContext.Facilities
            .AsNoTracking()
            .AnyAsync(
                facility => facility.FacilityId != request.FacilityId
                            && facility.FacilityCode == facilityCode,
                cancellationToken);

        if (duplicateCodeExists)
        {
            return FacilitySaveResult.ValidationFailed(new Dictionary<string, string[]>
            {
                [nameof(FacilitySaveRequest.FacilityCode)] = ["Facility code must be unique."]
            });
        }

        var now = DateTime.UtcNow;
        Facility facility;

        if (request.FacilityId.HasValue)
        {
            facility = await dbContext.Facilities
                .SingleOrDefaultAsync(existing => existing.FacilityId == request.FacilityId.Value, cancellationToken)
                ?? throw new InvalidOperationException($"Facility {request.FacilityId.Value} was not found.");

            facility.UpdateDetails(
                facilityCode,
                request.Name.Trim(),
                request.TimeZoneId.Trim(),
                request.IsActive,
                request.Notes,
                now);
        }
        else
        {
            facility = new Facility(
                facilityCode,
                request.Name.Trim(),
                request.TimeZoneId.Trim(),
                now,
                now,
                request.IsActive,
                request.Notes);

            dbContext.Facilities.Add(facility);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return FacilitySaveResult.Success(new FacilityAdminListItem(
            facility.FacilityId,
            facility.FacilityCode.Value,
            facility.Name,
            facility.TimeZoneId,
            facility.IsActive,
            facility.Notes,
            facility.CreatedUtc,
            facility.ModifiedUtc));
    }

    private static Dictionary<string, string[]> ValidateRequest(FacilitySaveRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.FacilityCode))
        {
            errors[nameof(FacilitySaveRequest.FacilityCode)] = ["Facility code is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(FacilitySaveRequest.Name)] = ["Facility name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.TimeZoneId))
        {
            errors[nameof(FacilitySaveRequest.TimeZoneId)] = ["Time zone is required."];
        }
        else if (!IsKnownTimeZone(request.TimeZoneId.Trim()))
        {
            errors[nameof(FacilitySaveRequest.TimeZoneId)] = ["Enter a valid system time zone ID."];
        }

        return errors;
    }

    private static bool IsKnownTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }
}

public sealed record FacilityAdminListItem(
    int FacilityId,
    string FacilityCode,
    string Name,
    string TimeZoneId,
    bool IsActive,
    string? Notes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc);

public sealed record FacilitySaveRequest(
    int? FacilityId,
    string FacilityCode,
    string Name,
    string TimeZoneId,
    bool IsActive,
    string? Notes);

public sealed record FacilitySaveResult(
    FacilitySaveStatus Status,
    FacilityAdminListItem? Facility,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static FacilitySaveResult Success(FacilityAdminListItem facility) =>
        new(FacilitySaveStatus.Success, facility, new Dictionary<string, string[]>());

    public static FacilitySaveResult ValidationFailed(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(FacilitySaveStatus.ValidationFailed, null, validationErrors);

    public static FacilitySaveResult Forbidden() =>
        new(FacilitySaveStatus.Forbidden, null, new Dictionary<string, string[]>());
}

public enum FacilitySaveStatus
{
    Success = 1,
    ValidationFailed = 2,
    Forbidden = 3
}
