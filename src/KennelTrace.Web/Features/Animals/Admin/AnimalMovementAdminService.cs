using System.Data;
using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Animals.Admin;

public interface IAnimalMovementAdminService
{
    Task<RecordAnimalStayResult> RecordStayAsync(
        RecordAnimalStayRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}

public sealed class AnimalMovementAdminService(
    KennelTraceDbContext dbContext,
    IAuthorizationService authorizationService) : IAnimalMovementAdminService
{
    public async Task<RecordAnimalStayResult> RecordStayAsync(
        RecordAnimalStayRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return RecordAnimalStayResult.Forbidden();
        }

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return RecordAnimalStayResult.ValidationFailed(validationErrors);
        }

        var now = DateTime.UtcNow;
        var recordedByUserId = ResolveRecordedByUserId(user);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        var animalExists = await dbContext.Animals
            .AsNoTracking()
            .AnyAsync(x => x.AnimalId == request.AnimalId, cancellationToken);

        if (!animalExists)
        {
            validationErrors[nameof(RecordAnimalStayRequest.AnimalId)] = ["Select a valid animal before recording a stay."];
        }

        var locationExists = await dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.LocationId == request.LocationId, cancellationToken);

        if (!locationExists)
        {
            validationErrors[nameof(RecordAnimalStayRequest.LocationId)] = ["Select a valid location before recording a stay."];
        }

        if (validationErrors.Count > 0)
        {
            return RecordAnimalStayResult.ValidationFailed(validationErrors);
        }

        var existingStays = await dbContext.MovementEvents
            .Where(x => x.AnimalId == request.AnimalId)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.MovementEventId)
            .ToListAsync(cancellationToken);

        if (CountOpenStays(existingStays) > 1)
        {
            return RecordAnimalStayResult.ValidationFailed(CreateOpenStayValidationErrors());
        }

        var openStay = existingStays.SingleOrDefault(x => x.IsOpen);
        var shouldCloseOpenStay = openStay is not null && request.StartUtc > openStay.StartUtc;

        if (shouldCloseOpenStay)
        {
            openStay!.Close(request.StartUtc, now);
        }

        if (FindOverlappingStay(existingStays, request.StartUtc, request.EndUtc) is not null)
        {
            return RecordAnimalStayResult.ValidationFailed(CreateOverlapValidationErrors());
        }

        if (shouldCloseOpenStay)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var stay = new MovementEvent(
            request.AnimalId,
            request.LocationId,
            request.StartUtc,
            now,
            now,
            request.EndUtc,
            request.MovementReason,
            SourceType.Manual,
            recordedByUserId,
            request.Notes);

        dbContext.MovementEvents.Add(stay);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsOpenStayViolation(exception))
        {
            return RecordAnimalStayResult.ValidationFailed(CreateOpenStayValidationErrors());
        }

        var persistedStays = await dbContext.MovementEvents
            .Where(x => x.AnimalId == request.AnimalId)
            .OrderBy(x => x.StartUtc)
            .ThenBy(x => x.MovementEventId)
            .ToListAsync(cancellationToken);

        if (CountOpenStays(persistedStays) > 1)
        {
            return RecordAnimalStayResult.ValidationFailed(CreateOpenStayValidationErrors());
        }

        if (HasOverlap(persistedStays))
        {
            return RecordAnimalStayResult.ValidationFailed(CreateOverlapValidationErrors());
        }

        await transaction.CommitAsync(cancellationToken);

        return RecordAnimalStayResult.Success(new AnimalMovementAdminRecord(
            stay.MovementEventId,
            stay.AnimalId,
            stay.LocationId,
            stay.StartUtc,
            stay.EndUtc,
            stay.MovementReason,
            stay.RecordedByUserId,
            stay.Notes,
            stay.CreatedUtc,
            stay.ModifiedUtc));
    }

    private static Dictionary<string, string[]> ValidateRequest(RecordAnimalStayRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.AnimalId <= 0)
        {
            errors[nameof(RecordAnimalStayRequest.AnimalId)] = ["Select an animal before recording a stay."];
        }

        if (request.LocationId <= 0)
        {
            errors[nameof(RecordAnimalStayRequest.LocationId)] = ["Select a location before recording a stay."];
        }

        if (request.StartUtc == default)
        {
            errors[nameof(RecordAnimalStayRequest.StartUtc)] = ["StartUtc is required."];
        }
        else if (request.StartUtc.Kind != DateTimeKind.Utc)
        {
            errors[nameof(RecordAnimalStayRequest.StartUtc)] = ["StartUtc must use UTC."];
        }

        if (request.EndUtc is null)
        {
            return errors;
        }

        if (request.EndUtc.Value.Kind != DateTimeKind.Utc)
        {
            errors[nameof(RecordAnimalStayRequest.EndUtc)] = ["EndUtc must use UTC."];
            return errors;
        }

        if (!errors.ContainsKey(nameof(RecordAnimalStayRequest.StartUtc)) && request.EndUtc.Value <= request.StartUtc)
        {
            errors[nameof(RecordAnimalStayRequest.EndUtc)] = ["EndUtc must be greater than StartUtc."];
        }

        return errors;
    }

    private static string? ResolveRecordedByUserId(ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId.Trim();
        }

        return string.IsNullOrWhiteSpace(user.Identity?.Name)
            ? null
            : user.Identity.Name.Trim();
    }

    private static MovementEvent? FindOverlappingStay(IEnumerable<MovementEvent> stays, DateTime startUtc, DateTime? endUtc)
    {
        return stays.FirstOrDefault(stay => MovementEvent.IntervalsOverlap(stay.StartUtc, stay.EndUtc, startUtc, endUtc));
    }

    private static bool HasOverlap(IReadOnlyList<MovementEvent> stays)
    {
        for (var i = 0; i < stays.Count; i++)
        {
            for (var j = i + 1; j < stays.Count; j++)
            {
                if (MovementEvent.IntervalsOverlap(stays[i].StartUtc, stays[i].EndUtc, stays[j].StartUtc, stays[j].EndUtc))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountOpenStays(IEnumerable<MovementEvent> stays) =>
        stays.Count(stay => stay.IsOpen);

    private static Dictionary<string, string[]> CreateOverlapValidationErrors() =>
        new()
        {
            [nameof(RecordAnimalStayRequest.StartUtc)] = ["The requested stay overlaps existing movement history for this animal."]
        };

    private static Dictionary<string, string[]> CreateOpenStayValidationErrors() =>
        new()
        {
            [nameof(RecordAnimalStayRequest.EndUtc)] = ["At most one open/current stay can exist per animal."]
        };

    private static bool IsOpenStayViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException sqlException
        && sqlException.Message.Contains("UX_MovementEvents_OneOpenStayPerAnimal", StringComparison.Ordinal);
}

public sealed record RecordAnimalStayRequest(
    int AnimalId,
    int LocationId,
    DateTime StartUtc,
    DateTime? EndUtc,
    string? MovementReason,
    string? Notes);

public sealed record AnimalMovementAdminRecord(
    long MovementEventId,
    int AnimalId,
    int LocationId,
    DateTime StartUtc,
    DateTime? EndUtc,
    string? MovementReason,
    string? RecordedByUserId,
    string? Notes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc);

public sealed record RecordAnimalStayResult(
    RecordAnimalStayStatus Status,
    AnimalMovementAdminRecord? Stay,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static RecordAnimalStayResult Success(AnimalMovementAdminRecord stay) =>
        new(RecordAnimalStayStatus.Success, stay, new Dictionary<string, string[]>());

    public static RecordAnimalStayResult ValidationFailed(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(RecordAnimalStayStatus.ValidationFailed, null, validationErrors);

    public static RecordAnimalStayResult Forbidden() =>
        new(RecordAnimalStayStatus.Forbidden, null, new Dictionary<string, string[]>());
}

public enum RecordAnimalStayStatus
{
    Success = 1,
    ValidationFailed = 2,
    Forbidden = 3
}
