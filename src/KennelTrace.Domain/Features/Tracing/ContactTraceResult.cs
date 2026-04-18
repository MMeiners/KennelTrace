using KennelTrace.Domain.Common;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ContactTraceResult
{
    public ContactTraceResult(
        int diseaseTraceProfileId,
        IEnumerable<long> sourceStayIds,
        IEnumerable<ImpactedLocationResult> impactedLocations,
        IEnumerable<ImpactedAnimalResult> impactedAnimals,
        bool usesPartialGraphData = false)
    {
        DiseaseTraceProfileId = Guard.Positive(diseaseTraceProfileId, nameof(diseaseTraceProfileId));

        SourceStayIds = NormalizePositiveIds(sourceStayIds, nameof(sourceStayIds));
        ImpactedLocations = NormalizeRequiredList(impactedLocations, nameof(impactedLocations));
        ImpactedAnimals = NormalizeRequiredList(impactedAnimals, nameof(impactedAnimals));
        UsesPartialGraphData = usesPartialGraphData;
    }

    public int DiseaseTraceProfileId { get; }

    public IReadOnlyList<long> SourceStayIds { get; }

    public IReadOnlyList<ImpactedLocationResult> ImpactedLocations { get; }

    public IReadOnlyList<ImpactedAnimalResult> ImpactedAnimals { get; }

    public bool UsesPartialGraphData { get; }

    private static IReadOnlyList<T> NormalizeRequiredList<T>(IEnumerable<T> values, string paramName)
    {
        var normalized = values?.ToArray() ?? throw new DomainValidationException($"{paramName} is required.");
        return normalized;
    }

    private static IReadOnlyList<long> NormalizePositiveIds(IEnumerable<long> values, string paramName)
    {
        var normalized = values?.Distinct().ToArray() ?? throw new DomainValidationException($"{paramName} is required.");
        Guard.Against(normalized.Length == 0, $"{paramName} must contain at least one value.");
        Guard.Against(normalized.Any(x => x <= 0), $"{paramName} must contain only positive values.");
        return normalized;
    }
}
