using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Locations.Admin;

public interface ILocationLinkAdminService
{
    Task<LocationLinkSaveResult> SaveAsync(LocationLinkSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    Task<LocationLinkRemoveResult> RemoveAsync(LocationLinkRemoveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class LocationLinkAdminService(
    KennelTraceDbContext dbContext,
    IAuthorizationService authorizationService) : ILocationLinkAdminService
{
    public async Task<LocationLinkSaveResult> SaveAsync(LocationLinkSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return LocationLinkSaveResult.Forbidden();
        }

        var validationErrors = ValidateRequestBasics(request);
        var facilityExists = await dbContext.Facilities
            .AsNoTracking()
            .AnyAsync(x => x.FacilityId == request.FacilityId, cancellationToken);

        if (!facilityExists)
        {
            validationErrors[nameof(LocationLinkSaveRequest.FacilityId)] = ["Select a valid facility before saving a link."];
        }

        var facilityLocations = facilityExists
            ? await dbContext.Locations
                .Where(x => x.FacilityId == request.FacilityId)
                .ToListAsync(cancellationToken)
            : [];

        var locationsById = facilityLocations.ToDictionary(x => x.LocationId);
        var fromLocation = locationsById.GetValueOrDefault(request.FromLocationId);
        var toLocation = locationsById.GetValueOrDefault(request.ToLocationId);

        if (fromLocation is null)
        {
            validationErrors[nameof(LocationLinkSaveRequest.FromLocationId)] = ["Source location must belong to the selected facility."];
        }

        if (toLocation is null)
        {
            validationErrors[nameof(LocationLinkSaveRequest.ToLocationId)] = ["Target location must belong to the selected facility."];
        }

        if (fromLocation is not null && !fromLocation.IsActive)
        {
            validationErrors[nameof(LocationLinkSaveRequest.FromLocationId)] = ["Links can only be created for active locations in this workflow."];
        }

        if (toLocation is not null && !toLocation.IsActive)
        {
            validationErrors[nameof(LocationLinkSaveRequest.ToLocationId)] = ["Links can only be created for active locations in this workflow."];
        }

        if (fromLocation is not null && toLocation is not null)
        {
            ValidateEndpointFamilyRules(request, fromLocation, toLocation, validationErrors);
        }

        var existingLinks = facilityExists
            ? await dbContext.LocationLinks
                .Where(x => x.FacilityId == request.FacilityId
                            && ((x.FromLocationId == request.FromLocationId
                                 && x.ToLocationId == request.ToLocationId
                                 && x.LinkType == request.LinkType)
                                || (x.FromLocationId == request.ToLocationId
                                    && x.ToLocationId == request.FromLocationId
                                    && x.LinkType == LinkTypeRules.InverseOf(request.LinkType))))
                .ToListAsync(cancellationToken)
            : [];

        var activeDirectedDuplicate = existingLinks.Any(x =>
            x.FromLocationId == request.FromLocationId
            && x.ToLocationId == request.ToLocationId
            && x.LinkType == request.LinkType
            && x.IsActive);

        if (activeDirectedDuplicate)
        {
            validationErrors[nameof(LocationLinkSaveRequest.LinkType)] = ["An active link with the same source, target, and type already exists."];
        }

        if (validationErrors.Count > 0)
        {
            return LocationLinkSaveResult.ValidationFailed(validationErrors);
        }

        var inverseType = LinkTypeRules.InverseOf(request.LinkType);
        var now = DateTime.UtcNow;

        var directed = existingLinks.SingleOrDefault(x =>
            x.FromLocationId == request.FromLocationId
            && x.ToLocationId == request.ToLocationId
            && x.LinkType == request.LinkType);

        if (directed is null)
        {
            directed = new LocationLink(
                request.FacilityId,
                request.FromLocationId,
                request.ToLocationId,
                request.LinkType,
                now,
                now,
                sourceType: SourceType.Manual,
                sourceReference: request.SourceReference,
                notes: request.Notes);
            dbContext.LocationLinks.Add(directed);
        }
        else
        {
            directed.Activate(SourceType.Manual, request.SourceReference, request.Notes, now);
        }

        var reciprocal = existingLinks.SingleOrDefault(x =>
            x.FromLocationId == request.ToLocationId
            && x.ToLocationId == request.FromLocationId
            && x.LinkType == inverseType);

        if (reciprocal is null)
        {
            reciprocal = new LocationLink(
                request.FacilityId,
                request.ToLocationId,
                request.FromLocationId,
                inverseType,
                now,
                now,
                sourceType: SourceType.Manual,
                sourceReference: request.SourceReference,
                notes: request.Notes);
            dbContext.LocationLinks.Add(reciprocal);
        }
        else
        {
            reciprocal.Activate(SourceType.Manual, request.SourceReference, request.Notes, now);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsActiveDirectedDuplicate(exception))
        {
            return LocationLinkSaveResult.ValidationFailed(new Dictionary<string, string[]>
            {
                [nameof(LocationLinkSaveRequest.LinkType)] =
                [
                    "An active link with the same source, target, and type already exists."
                ]
            });
        }

        return LocationLinkSaveResult.Success();
    }

    public async Task<LocationLinkRemoveResult> RemoveAsync(LocationLinkRemoveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return LocationLinkRemoveResult.Forbidden();
        }

        var inverseType = LinkTypeRules.InverseOf(request.LinkType);
        var links = await dbContext.LocationLinks
            .Where(x => x.FacilityId == request.FacilityId
                        && ((x.FromLocationId == request.FromLocationId
                             && x.ToLocationId == request.ToLocationId
                             && x.LinkType == request.LinkType)
                            || (x.FromLocationId == request.ToLocationId
                                && x.ToLocationId == request.FromLocationId
                                && x.LinkType == inverseType)))
            .ToListAsync(cancellationToken);

        var activeLinks = links.Where(x => x.IsActive).ToList();
        if (activeLinks.Count == 0)
        {
            return LocationLinkRemoveResult.NotFound();
        }

        var now = DateTime.UtcNow;
        foreach (var link in activeLinks)
        {
            link.Deactivate(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocationLinkRemoveResult.Success();
    }

    private static Dictionary<string, string[]> ValidateRequestBasics(LocationLinkSaveRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.FacilityId <= 0)
        {
            errors[nameof(LocationLinkSaveRequest.FacilityId)] = ["Select a facility before saving a link."];
        }

        if (request.FromLocationId <= 0)
        {
            errors[nameof(LocationLinkSaveRequest.FromLocationId)] = ["Select a source location before saving a link."];
        }

        if (request.ToLocationId <= 0)
        {
            errors[nameof(LocationLinkSaveRequest.ToLocationId)] = ["Select a target location before saving a link."];
        }

        if (request.FromLocationId > 0 && request.FromLocationId == request.ToLocationId)
        {
            errors[nameof(LocationLinkSaveRequest.ToLocationId)] = ["Self-links are invalid."];
        }

        return errors;
    }

    private static void ValidateEndpointFamilyRules(
        LocationLinkSaveRequest request,
        Location fromLocation,
        Location toLocation,
        IDictionary<string, string[]> validationErrors)
    {
        if (LinkTypeRules.IsAdjacency(request.LinkType))
        {
            if (fromLocation.LocationType != LocationType.Kennel || toLocation.LocationType != LocationType.Kennel)
            {
                validationErrors[nameof(LocationLinkSaveRequest.LinkType)] = ["Adjacency links must connect kennel locations."];
            }

            return;
        }

        if (request.AllowTopologyEndpointOverride)
        {
            return;
        }

        if (!LocationTypeRules.IsNonKennelSpace(fromLocation.LocationType) || !LocationTypeRules.IsNonKennelSpace(toLocation.LocationType))
        {
            validationErrors[nameof(LocationLinkSaveRequest.AllowTopologyEndpointOverride)] =
            [
                "Topology links default to non-kennel spaces. Enable the explicit override to save an unusual topology link."
            ];
        }
    }

    private static bool IsActiveDirectedDuplicate(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Message.Contains("UX_LocationLinks_ActiveDirected", StringComparison.Ordinal);
}

public sealed record LocationLinkSaveRequest(
    int FacilityId,
    int FromLocationId,
    int ToLocationId,
    LinkType LinkType,
    bool AllowTopologyEndpointOverride,
    string? SourceReference,
    string? Notes);

public sealed record LocationLinkSaveResult(
    LocationLinkSaveStatus Status,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static LocationLinkSaveResult Success() =>
        new(LocationLinkSaveStatus.Success, new Dictionary<string, string[]>());

    public static LocationLinkSaveResult ValidationFailed(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(LocationLinkSaveStatus.ValidationFailed, validationErrors);

    public static LocationLinkSaveResult Forbidden() =>
        new(LocationLinkSaveStatus.Forbidden, new Dictionary<string, string[]>());
}

public enum LocationLinkSaveStatus
{
    Success = 1,
    ValidationFailed = 2,
    Forbidden = 3
}

public sealed record LocationLinkRemoveRequest(
    int FacilityId,
    int FromLocationId,
    int ToLocationId,
    LinkType LinkType);

public sealed record LocationLinkRemoveResult(LocationLinkRemoveStatus Status)
{
    public static LocationLinkRemoveResult Success() => new(LocationLinkRemoveStatus.Success);

    public static LocationLinkRemoveResult Forbidden() => new(LocationLinkRemoveStatus.Forbidden);

    public static LocationLinkRemoveResult NotFound() => new(LocationLinkRemoveStatus.NotFound);
}

public enum LocationLinkRemoveStatus
{
    Success = 1,
    Forbidden = 2,
    NotFound = 3
}
