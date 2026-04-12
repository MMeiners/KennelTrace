using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class DiseaseTraceProfile
{
    public DiseaseTraceProfile(
        Guid id,
        Guid diseaseId,
        string displayName,
        int defaultLookbackHours,
        bool includeSameLocation,
        bool includeSameRoom,
        bool includeAdjacentKennels,
        int adjacencyDepth,
        IEnumerable<LinkType> includedLinkTypes,
        int topologyDepth,
        bool isActive = true)
    {
        Id = Guard.RequiredId(id, nameof(id));
        DiseaseId = Guard.RequiredId(diseaseId, nameof(diseaseId));
        DisplayName = Guard.RequiredText(displayName, nameof(displayName));
        DefaultLookbackHours = Guard.Positive(defaultLookbackHours, nameof(defaultLookbackHours));
        IncludeSameLocation = includeSameLocation;
        IncludeSameRoom = includeSameRoom;
        IncludeAdjacentKennels = includeAdjacentKennels;
        AdjacencyDepth = Guard.NonNegative(adjacencyDepth, nameof(adjacencyDepth));
        TopologyDepth = Guard.NonNegative(topologyDepth, nameof(topologyDepth));
        IsActive = isActive;

        var linkTypes = includedLinkTypes?.Distinct().ToArray() ?? [];
        Guard.Against(linkTypes.Any(LinkTypeRules.IsAdjacency), "DiseaseTraceProfile topology link types cannot include adjacency links.");
        IncludedLinkTypes = Array.AsReadOnly(linkTypes);
    }

    public Guid Id { get; }

    public Guid DiseaseId { get; }

    public string DisplayName { get; }

    public int DefaultLookbackHours { get; }

    public bool IncludeSameLocation { get; }

    public bool IncludeSameRoom { get; }

    public bool IncludeAdjacentKennels { get; }

    public int AdjacencyDepth { get; }

    public IReadOnlyCollection<LinkType> IncludedLinkTypes { get; }

    public int TopologyDepth { get; }

    public bool IsActive { get; }
}
