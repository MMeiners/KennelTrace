using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Animals;

public sealed class MovementEvent
{
    private MovementEvent()
    {
    }

    public MovementEvent(
        int animalId,
        int locationId,
        DateTime startUtc,
        DateTime createdUtc,
        DateTime modifiedUtc,
        DateTime? endUtc = null,
        string? movementReason = null,
        SourceType sourceType = SourceType.Manual,
        string? recordedByUserId = null,
        string? notes = null)
    {
        Guard.Against(animalId <= 0, "animalId is required.");
        Guard.Against(locationId <= 0, "locationId is required.");

        AnimalId = animalId;
        LocationId = locationId;
        StartUtc = Guard.RequiredUtc(startUtc, nameof(startUtc));
        EndUtc = endUtc is null ? null : Guard.RequiredUtc(endUtc.Value, nameof(endUtc));
        Guard.Against(EndUtc is not null && EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
        MovementReason = string.IsNullOrWhiteSpace(movementReason) ? null : movementReason.Trim();
        SourceType = sourceType;
        RecordedByUserId = string.IsNullOrWhiteSpace(recordedByUserId) ? null : recordedByUserId.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public long MovementEventId { get; private set; }

    public int AnimalId { get; private set; }

    public int LocationId { get; private set; }

    public DateTime StartUtc { get; private set; }

    public DateTime? EndUtc { get; private set; }

    public string? MovementReason { get; private set; }

    public SourceType SourceType { get; private set; }

    public string? RecordedByUserId { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public bool IsOpen => EndUtc is null;

    public void Close(DateTime endUtc, DateTime modifiedUtc)
    {
        EndUtc = Guard.RequiredUtc(endUtc, nameof(endUtc));
        Guard.Against(EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
    }

    public void UpdateNotes(string? notes, DateTime modifiedUtc)
    {
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));
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
