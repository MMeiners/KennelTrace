using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Locations.Admin;

public interface ILocationAdminService
{
    Task<LocationAdminFacilityView?> GetFacilityAsync(int facilityId, CancellationToken cancellationToken = default);

    Task<LocationSaveResult> SaveAsync(LocationSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class LocationAdminService(
    KennelTraceDbContext dbContext,
    IAuthorizationService authorizationService) : ILocationAdminService
{
    public async Task<LocationAdminFacilityView?> GetFacilityAsync(int facilityId, CancellationToken cancellationToken = default)
    {
        var facility = await dbContext.Facilities
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId)
            .Select(x => new
            {
                x.FacilityId,
                FacilityCode = x.FacilityCode.Value,
                x.Name,
                x.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (facility is null)
        {
            return null;
        }

        var locations = await dbContext.Locations
            .AsNoTracking()
            .Where(x => x.FacilityId == facilityId)
            .Select(x => new LocationAdminListItem(
                x.LocationId,
                x.FacilityId,
                x.ParentLocationId,
                x.LocationType,
                x.LocationCode.Value,
                x.Name,
                x.DisplayOrder,
                x.IsActive,
                x.Notes))
            .ToListAsync(cancellationToken);

        var orderedLocations = OrderLocations(locations);
        var locationsByParent = orderedLocations.ToLookup(x => x.ParentLocationId);

        return new LocationAdminFacilityView(
            facility.FacilityId,
            facility.FacilityCode,
            facility.Name,
            facility.IsActive,
            orderedLocations,
            BuildTree(null, locationsByParent));
    }

    public async Task<LocationSaveResult> SaveAsync(LocationSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return LocationSaveResult.Forbidden();
        }

        var validationErrors = ValidateRequestBasics(request);

        LocationCode? locationCode = null;
        if (!validationErrors.ContainsKey(nameof(LocationSaveRequest.LocationCode)))
        {
            try
            {
                locationCode = new LocationCode(request.LocationCode.Trim());
            }
            catch (DomainValidationException exception)
            {
                validationErrors[nameof(LocationSaveRequest.LocationCode)] = [exception.Message];
            }
        }

        var facilityExists = await dbContext.Facilities
            .AsNoTracking()
            .AnyAsync(x => x.FacilityId == request.FacilityId, cancellationToken);

        if (!facilityExists)
        {
            validationErrors[nameof(LocationSaveRequest.FacilityId)] = ["Select a valid facility before saving a location."];
        }

        var facilityLocations = facilityExists
            ? await dbContext.Locations
                .Where(x => x.FacilityId == request.FacilityId)
                .ToListAsync(cancellationToken)
            : [];

        var existingLocation = request.LocationId.HasValue
            ? facilityLocations.SingleOrDefault(x => x.LocationId == request.LocationId.Value)
            : null;

        if (request.LocationId.HasValue && existingLocation is null)
        {
            validationErrors[nameof(LocationSaveRequest.LocationId)] = ["The selected location no longer exists for this facility."];
        }

        if (locationCode is not null)
        {
            var duplicateCodeExists = facilityLocations.Any(x =>
                x.LocationId != request.LocationId &&
                x.LocationCode == locationCode);

            if (duplicateCodeExists)
            {
                validationErrors[nameof(LocationSaveRequest.LocationCode)] = ["Location code must be unique within the facility."];
            }
        }

        ValidateContainmentRules(request, facilityLocations, validationErrors);

        if (validationErrors.Count > 0 || locationCode is null)
        {
            return LocationSaveResult.ValidationFailed(validationErrors);
        }

        var validatedLocationCode = locationCode.Value;

        var now = DateTime.UtcNow;
        Location location;

        if (existingLocation is null)
        {
            location = new Location(
                request.FacilityId,
                request.LocationType,
                validatedLocationCode,
                request.Name.Trim(),
                now,
                now,
                request.ParentLocationId,
                request.IsActive,
                displayOrder: request.DisplayOrder,
                notes: request.Notes);

            dbContext.Locations.Add(location);
        }
        else
        {
            location = existingLocation;
            location.UpdateDetails(
                request.LocationType,
                validatedLocationCode,
                request.Name.Trim(),
                request.ParentLocationId,
                request.DisplayOrder,
                request.IsActive,
                request.Notes,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return LocationSaveResult.Success(new LocationAdminListItem(
            location.LocationId,
            location.FacilityId,
            location.ParentLocationId,
            location.LocationType,
            location.LocationCode.Value,
            location.Name,
            location.DisplayOrder,
            location.IsActive,
            location.Notes));
    }

    private static Dictionary<string, string[]> ValidateRequestBasics(LocationSaveRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.FacilityId <= 0)
        {
            errors[nameof(LocationSaveRequest.FacilityId)] = ["Select a facility before saving a location."];
        }

        if (string.IsNullOrWhiteSpace(request.LocationCode))
        {
            errors[nameof(LocationSaveRequest.LocationCode)] = ["Location code is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            errors[nameof(LocationSaveRequest.Name)] = ["Location name is required."];
        }

        return errors;
    }

    private static void ValidateContainmentRules(
        LocationSaveRequest request,
        IReadOnlyList<Location> facilityLocations,
        IDictionary<string, string[]> validationErrors)
    {
        var locationsById = facilityLocations.ToDictionary(x => x.LocationId);
        Location? parent = null;

        if (request.ParentLocationId.HasValue)
        {
            if (request.LocationId.HasValue && request.ParentLocationId.Value == request.LocationId.Value)
            {
                validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = ["A location cannot parent itself."];
                return;
            }

            if (!locationsById.TryGetValue(request.ParentLocationId.Value, out parent))
            {
                validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = ["Parent and child must belong to the same facility."];
                return;
            }

            if (!LocationTypeRules.CanContainChild(parent.LocationType, request.LocationType))
            {
                validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = request.LocationType == LocationType.Kennel
                    ? ["Kennels must have a valid room-like parent."]
                    : ["The selected parent cannot contain this location type."];
                return;
            }
        }
        else if (LocationTypeRules.RequiresContainmentParent(request.LocationType))
        {
            validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = ["Kennels must have a valid room-like parent."];
            return;
        }
        else if (!LocationTypeRules.CanAppearAtFacilityRoot(request.LocationType))
        {
            validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = ["The selected location type requires a valid parent."];
            return;
        }

        if (request.LocationId.HasValue && request.ParentLocationId.HasValue && CreatesCycle(request.LocationId.Value, request.ParentLocationId.Value, locationsById))
        {
            validationErrors[nameof(LocationSaveRequest.ParentLocationId)] = ["Parent chains must not be cyclic."];
            return;
        }

        if (!request.LocationId.HasValue)
        {
            return;
        }

        var childTypes = facilityLocations
            .Where(x => x.ParentLocationId == request.LocationId.Value)
            .Select(x => x.LocationType)
            .ToList();

        if (childTypes.Count == 0)
        {
            return;
        }

        if (!LocationTypeRules.CanBeContainmentParent(request.LocationType))
        {
            validationErrors[nameof(LocationSaveRequest.LocationType)] = ["Locations with child locations must remain a valid room-like parent type."];
            return;
        }

        if (childTypes.Any(childType => !LocationTypeRules.CanContainChild(request.LocationType, childType)))
        {
            validationErrors[nameof(LocationSaveRequest.LocationType)] = ["The selected location type is not allowed with the existing child locations."];
        }
    }

    private static bool CreatesCycle(int locationId, int parentLocationId, IReadOnlyDictionary<int, Location> locationsById)
    {
        var visited = new HashSet<int>();
        var currentParentId = parentLocationId;

        while (visited.Add(currentParentId))
        {
            if (currentParentId == locationId)
            {
                return true;
            }

            if (!locationsById.TryGetValue(currentParentId, out var currentParent) || currentParent.ParentLocationId is null)
            {
                return false;
            }

            currentParentId = currentParent.ParentLocationId.Value;
        }

        return false;
    }

    private static List<LocationAdminListItem> OrderLocations(IEnumerable<LocationAdminListItem> locations) =>
        locations
            .OrderBy(x => x.ParentLocationId.HasValue ? 1 : 0)
            .ThenBy(x => x.DisplayOrder ?? int.MaxValue)
            .ThenBy(x => x.Name)
            .ThenBy(x => x.LocationCode)
            .ToList();

    private static IReadOnlyList<LocationAdminTreeItem> BuildTree(
        int? parentLocationId,
        ILookup<int?, LocationAdminListItem> locationsByParent)
    {
        var children = locationsByParent[parentLocationId];
        if (!children.Any())
        {
            return [];
        }

        return children
            .Select(child => new LocationAdminTreeItem(
                child.LocationId,
                child.FacilityId,
                child.ParentLocationId,
                child.LocationType,
                child.LocationCode,
                child.Name,
                child.DisplayOrder,
                child.IsActive,
                child.Notes,
                BuildTree(child.LocationId, locationsByParent)))
            .ToList();
    }
}

public sealed record LocationAdminFacilityView(
    int FacilityId,
    string FacilityCode,
    string FacilityName,
    bool IsActive,
    IReadOnlyList<LocationAdminListItem> Locations,
    IReadOnlyList<LocationAdminTreeItem> RootLocations);

public sealed record LocationAdminListItem(
    int LocationId,
    int FacilityId,
    int? ParentLocationId,
    LocationType LocationType,
    string LocationCode,
    string Name,
    int? DisplayOrder,
    bool IsActive,
    string? Notes);

public sealed record LocationAdminTreeItem(
    int LocationId,
    int FacilityId,
    int? ParentLocationId,
    LocationType LocationType,
    string LocationCode,
    string Name,
    int? DisplayOrder,
    bool IsActive,
    string? Notes,
    IReadOnlyList<LocationAdminTreeItem> Children);

public sealed record LocationSaveRequest(
    int? LocationId,
    int FacilityId,
    LocationType LocationType,
    string LocationCode,
    string Name,
    int? ParentLocationId,
    int? DisplayOrder,
    bool IsActive,
    string? Notes);

public sealed record LocationSaveResult(
    LocationSaveStatus Status,
    LocationAdminListItem? Location,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static LocationSaveResult Success(LocationAdminListItem location) =>
        new(LocationSaveStatus.Success, location, new Dictionary<string, string[]>());

    public static LocationSaveResult ValidationFailed(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(LocationSaveStatus.ValidationFailed, null, validationErrors);

    public static LocationSaveResult Forbidden() =>
        new(LocationSaveStatus.Forbidden, null, new Dictionary<string, string[]>());
}

public enum LocationSaveStatus
{
    Success = 1,
    ValidationFailed = 2,
    Forbidden = 3
}
