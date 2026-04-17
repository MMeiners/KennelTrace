using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Animals;

public sealed class Animal
{
    private Animal()
    {
    }

    public Animal(
        AnimalCode animalNumber,
        DateTime createdUtc,
        DateTime modifiedUtc,
        string? name = null,
        string species = "Dog",
        string? sex = null,
        string? breed = null,
        DateOnly? dateOfBirth = null,
        bool isActive = true,
        string? notes = null)
    {
        AnimalNumber = animalNumber;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Species = Guard.RequiredText(species, nameof(species));
        Sex = string.IsNullOrWhiteSpace(sex) ? null : sex.Trim();
        Breed = string.IsNullOrWhiteSpace(breed) ? null : breed.Trim();
        DateOfBirth = dateOfBirth;
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public int AnimalId { get; private set; }

    public AnimalCode AnimalNumber { get; private set; } = default!;

    public string? Name { get; private set; }

    public string Species { get; private set; } = null!;

    public string? Sex { get; private set; }

    public string? Breed { get; private set; }

    public DateOnly? DateOfBirth { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public void Rename(string? name, DateTime modifiedUtc)
    {
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void UpdateDetails(string species, string? sex, string? breed, DateOnly? dateOfBirth, string? notes, DateTime modifiedUtc)
    {
        Species = Guard.RequiredText(species, nameof(species));
        Sex = string.IsNullOrWhiteSpace(sex) ? null : sex.Trim();
        Breed = string.IsNullOrWhiteSpace(breed) ? null : breed.Trim();
        DateOfBirth = dateOfBirth;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void UpdateRecord(
        AnimalCode animalNumber,
        string? name,
        string species,
        string? sex,
        string? breed,
        DateOnly? dateOfBirth,
        bool isActive,
        string? notes,
        DateTime modifiedUtc)
    {
        AnimalNumber = animalNumber;
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Species = Guard.RequiredText(species, nameof(species));
        Sex = string.IsNullOrWhiteSpace(sex) ? null : sex.Trim();
        Breed = string.IsNullOrWhiteSpace(breed) ? null : breed.Trim();
        DateOfBirth = dateOfBirth;
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void Deactivate(DateTime modifiedUtc)
    {
        IsActive = false;
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }
}
