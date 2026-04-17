using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class TraceCandidateMovementStay
{
    public TraceCandidateMovementStay(
        long stayId,
        int animalId,
        AnimalCode animalNumber,
        int locationId,
        DateTime startUtc,
        DateTime? endUtc = null,
        string? animalName = null)
    {
        Guard.Against(stayId <= 0, "stayId must be greater than zero.");

        StayId = stayId;
        AnimalId = Guard.Positive(animalId, nameof(animalId));
        AnimalNumber = new AnimalCode(Guard.RequiredText(animalNumber.Value, nameof(animalNumber)));
        AnimalName = string.IsNullOrWhiteSpace(animalName) ? null : animalName.Trim();
        LocationId = Guard.Positive(locationId, nameof(locationId));
        StartUtc = Guard.RequiredUtc(startUtc, nameof(startUtc));
        EndUtc = endUtc is null ? null : Guard.RequiredUtc(endUtc.Value, nameof(endUtc));
        Guard.Against(EndUtc is not null && EndUtc <= StartUtc, "EndUtc must be greater than StartUtc.");
    }

    public long StayId { get; }

    public int AnimalId { get; }

    public AnimalCode AnimalNumber { get; }

    public string? AnimalName { get; }

    public int LocationId { get; }

    public DateTime StartUtc { get; }

    public DateTime? EndUtc { get; }

    public string AnimalSortNumber => AnimalNumber.Value;

    public string AnimalSortName => AnimalName ?? string.Empty;
}
