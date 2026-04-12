using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Animals;

public sealed class Animal
{
    public Animal(Guid id, AnimalCode code, string displayName, bool isActive = true)
    {
        Id = Guard.RequiredId(id, nameof(id));
        Code = code;
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
        IsActive = isActive;
    }

    public Guid Id { get; }

    public AnimalCode Code { get; }

    public string DisplayName { get; private set; }

    public bool IsActive { get; private set; }

    public void Rename(string displayName)
    {
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
