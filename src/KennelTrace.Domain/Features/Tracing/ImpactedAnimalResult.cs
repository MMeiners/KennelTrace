using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedAnimalResult
{
    public ImpactedAnimalResult(
        int animalId,
        AnimalCode animalNumber,
        string? animalName,
        int impactedLocationId,
        IEnumerable<ImpactedAnimalStayOverlap> overlappingStays,
        IEnumerable<ImpactedLocationReason> reasons)
    {
        AnimalId = Guard.Positive(animalId, nameof(animalId));
        AnimalNumber = new AnimalCode(Guard.RequiredText(animalNumber.Value, nameof(animalNumber)));
        AnimalName = string.IsNullOrWhiteSpace(animalName) ? null : animalName.Trim();
        ImpactedLocationId = Guard.Positive(impactedLocationId, nameof(impactedLocationId));

        OverlappingStays = NormalizeOverlappingStays(overlappingStays);
        OverlappingStayIds = OverlappingStays
            .Select(x => x.OverlappingStayId)
            .Distinct()
            .ToArray();

        Reasons = NormalizeReasons(reasons);
        ReasonCodes = Reasons
            .Select(x => x.ReasonCode)
            .Distinct()
            .ToArray();
    }

    public int AnimalId { get; }

    public AnimalCode AnimalNumber { get; }

    public string? AnimalName { get; }

    public string AnimalSortNumber => AnimalNumber.Value;

    public string AnimalSortName => AnimalName ?? string.Empty;

    public int ImpactedLocationId { get; }

    public IReadOnlyList<ImpactedAnimalStayOverlap> OverlappingStays { get; }

    public IReadOnlyList<long> OverlappingStayIds { get; }

    public IReadOnlyList<ImpactedLocationReason> Reasons { get; }

    public IReadOnlyList<TraceReasonCode> ReasonCodes { get; }

    private static IReadOnlyList<ImpactedAnimalStayOverlap> NormalizeOverlappingStays(IEnumerable<ImpactedAnimalStayOverlap> overlappingStays)
    {
        var normalized = overlappingStays?.ToArray() ?? throw new DomainValidationException("overlappingStays is required.");
        Guard.Against(normalized.Length == 0, "overlappingStays must contain at least one value.");

        return normalized
            .GroupBy(x => new
            {
                x.OverlappingStayId,
                x.SourceStayId
            })
            .Select(x => x.First())
            .OrderBy(x => x.OverlapStartUtc)
            .ThenBy(x => x.OverlapEndUtc)
            .ThenBy(x => x.StayStartUtc)
            .ThenBy(x => x.OverlappingStayId)
            .ThenBy(x => x.SourceStayId)
            .ToArray();
    }

    private static IReadOnlyList<ImpactedLocationReason> NormalizeReasons(IEnumerable<ImpactedLocationReason> reasons)
    {
        var normalized = reasons?.ToArray() ?? throw new DomainValidationException("reasons is required.");
        Guard.Against(normalized.Length == 0, "Impacted results must include at least one trace reason.");

        return normalized
            .GroupBy(x => new
            {
                x.ReasonCode,
                x.SourceLocationId,
                x.SourceStayId,
                x.MatchKind,
                x.ScopeLocationId,
                x.ViaLinkType,
                x.TraversalDepth
            })
            .Select(x => x.First())
            .OrderBy(x => x.ReasonCode)
            .ThenBy(x => x.MatchKind)
            .ThenBy(x => x.ScopeLocationId)
            .ThenBy(x => x.TraversalDepth)
            .ThenBy(x => x.ViaLinkType)
            .ThenBy(x => x.SourceLocationId)
            .ThenBy(x => x.SourceStayId)
            .ToArray();
    }
}
