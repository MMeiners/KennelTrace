using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Animals;

public sealed class MovementEvent
{
    public MovementEvent(
        Guid id,
        Guid animalId,
        Guid locationId,
        DateTime startUtc,
        DateTime? endUtc = null,
        string? notes = null)
    {
        Id = Guard.RequiredId(id, nameof(id));
        AnimalId = Guard.RequiredId(animalId, nameof(animalId));
        LocationId = Guard.RequiredId(locationId, nameof(locationId));
        StartUtc = Guard.RequiredUtc(startUtc, nameof(startUtc));
        EndUtc = endUtc is null ? null : Guard.RequiredUtc(endUtc.Value, nameof(endUtc));
        Guard.Against(EndUtc is not null && EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public Guid Id { get; }

    public Guid AnimalId { get; }

    public Guid LocationId { get; }

    public DateTime StartUtc { get; }

    public DateTime? EndUtc { get; private set; }

    public string? Notes { get; }

    public bool IsOpen => EndUtc is null;

    public void Close(DateTime endUtc)
    {
        EndUtc = Guard.RequiredUtc(endUtc, nameof(endUtc));
        Guard.Against(EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
    }

    public bool Overlaps(MovementEvent other)
    {
        return IntervalsOverlap(StartUtc, EndUtc, other.StartUtc, other.EndUtc);
    }

    public static bool IntervalsOverlap(
        DateTime firstStartUtc,
        DateTime? firstEndUtc,
        DateTime secondStartUtc,
        DateTime? secondEndUtc)
    {
        var normalizedFirstStart = Guard.RequiredUtc(firstStartUtc, nameof(firstStartUtc));
        var normalizedSecondStart = Guard.RequiredUtc(secondStartUtc, nameof(secondStartUtc));
        var normalizedFirstEnd = firstEndUtc is null ? DateTime.MaxValue : Guard.RequiredUtc(firstEndUtc.Value, nameof(firstEndUtc));
        var normalizedSecondEnd = secondEndUtc is null ? DateTime.MaxValue : Guard.RequiredUtc(secondEndUtc.Value, nameof(secondEndUtc));

        Guard.Against(normalizedFirstEnd <= normalizedFirstStart, "The first interval must have EndUtc greater than StartUtc.");
        Guard.Against(normalizedSecondEnd <= normalizedSecondStart, "The second interval must have EndUtc greater than StartUtc.");

        return normalizedFirstStart < normalizedSecondEnd && normalizedSecondStart < normalizedFirstEnd;
    }
}
