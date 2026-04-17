using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Features.Animals.Admin;

public interface IAnimalAdminService
{
    Task<AnimalSaveResult> SaveAsync(AnimalSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
}

public sealed class AnimalAdminService(
    KennelTraceDbContext dbContext,
    IAuthorizationService authorizationService) : IAnimalAdminService
{
    public async Task<AnimalSaveResult> SaveAsync(AnimalSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var authorizationResult = await authorizationService.AuthorizeAsync(user, resource: null, policyName: KennelTracePolicies.AdminOnly);
        if (!authorizationResult.Succeeded)
        {
            return AnimalSaveResult.Forbidden();
        }

        var validationErrors = ValidateRequest(request);
        AnimalCode? animalNumber = null;

        if (!validationErrors.ContainsKey(nameof(AnimalSaveRequest.AnimalNumber)))
        {
            try
            {
                animalNumber = new AnimalCode(request.AnimalNumber.Trim());
            }
            catch (DomainValidationException exception)
            {
                validationErrors[nameof(AnimalSaveRequest.AnimalNumber)] = [exception.Message];
            }
        }

        Animal? existingAnimal = null;
        if (request.AnimalId.HasValue)
        {
            existingAnimal = await dbContext.Animals
                .SingleOrDefaultAsync(x => x.AnimalId == request.AnimalId.Value, cancellationToken);

            if (existingAnimal is null)
            {
                validationErrors[nameof(AnimalSaveRequest.AnimalId)] = ["The selected animal no longer exists."];
            }
        }

        if (animalNumber is not null)
        {
            var duplicateAnimalNumberExists = await dbContext.Animals
                .AsNoTracking()
                .AnyAsync(
                    animal => animal.AnimalId != request.AnimalId
                              && animal.AnimalNumber == animalNumber,
                    cancellationToken);

            if (duplicateAnimalNumberExists)
            {
                validationErrors[nameof(AnimalSaveRequest.AnimalNumber)] = ["Animal number must be unique."];
            }
        }

        if (validationErrors.Count > 0 || animalNumber is null)
        {
            return AnimalSaveResult.ValidationFailed(validationErrors);
        }

        var now = DateTime.UtcNow;
        Animal animal;

        if (existingAnimal is null)
        {
            animal = new Animal(
                animalNumber.Value,
                now,
                now,
                request.Name,
                request.Species.Trim(),
                request.Sex,
                request.Breed,
                request.DateOfBirth,
                request.IsActive,
                request.Notes);

            dbContext.Animals.Add(animal);
        }
        else
        {
            animal = existingAnimal;
            animal.UpdateRecord(
                animalNumber.Value,
                request.Name,
                request.Species.Trim(),
                request.Sex,
                request.Breed,
                request.DateOfBirth,
                request.IsActive,
                request.Notes,
                now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return AnimalSaveResult.Success(new AnimalAdminRecord(
            animal.AnimalId,
            animal.AnimalNumber.Value,
            animal.Name,
            animal.Species,
            animal.Sex,
            animal.Breed,
            animal.DateOfBirth,
            animal.IsActive,
            animal.Notes,
            animal.CreatedUtc,
            animal.ModifiedUtc));
    }

    private static Dictionary<string, string[]> ValidateRequest(AnimalSaveRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.AnimalNumber))
        {
            errors[nameof(AnimalSaveRequest.AnimalNumber)] = ["Animal number is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Species))
        {
            errors[nameof(AnimalSaveRequest.Species)] = ["Species is required."];
        }

        return errors;
    }
}

public sealed record AnimalSaveRequest(
    int? AnimalId,
    string AnimalNumber,
    string? Name,
    string Species,
    string? Sex,
    string? Breed,
    DateOnly? DateOfBirth,
    bool IsActive,
    string? Notes);

public sealed record AnimalAdminRecord(
    int AnimalId,
    string AnimalNumber,
    string? Name,
    string Species,
    string? Sex,
    string? Breed,
    DateOnly? DateOfBirth,
    bool IsActive,
    string? Notes,
    DateTime CreatedUtc,
    DateTime ModifiedUtc);

public sealed record AnimalSaveResult(
    AnimalSaveStatus Status,
    AnimalAdminRecord? Animal,
    IReadOnlyDictionary<string, string[]> ValidationErrors)
{
    public static AnimalSaveResult Success(AnimalAdminRecord animal) =>
        new(AnimalSaveStatus.Success, animal, new Dictionary<string, string[]>());

    public static AnimalSaveResult ValidationFailed(IReadOnlyDictionary<string, string[]> validationErrors) =>
        new(AnimalSaveStatus.ValidationFailed, null, validationErrors);

    public static AnimalSaveResult Forbidden() =>
        new(AnimalSaveStatus.Forbidden, null, new Dictionary<string, string[]>());
}

public enum AnimalSaveStatus
{
    Success = 1,
    ValidationFailed = 2,
    Forbidden = 3
}
