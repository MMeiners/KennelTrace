using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedLocationExpansionSettings
{
    public ImpactedLocationExpansionSettings(
        bool includeSameLocation = true,
        bool includeSameRoom = true,
        int adjacencyDepth = 0,
        int topologyDepth = 0,
        IEnumerable<LinkType>? allowedTopologyLinkTypes = null)
    {
        IncludeSameLocation = includeSameLocation;
        IncludeSameRoom = includeSameRoom;
        AdjacencyDepth = Guard.NonNegative(adjacencyDepth, nameof(adjacencyDepth));
        TopologyDepth = Guard.NonNegative(topologyDepth, nameof(topologyDepth));

        AllowedTopologyLinkTypes = (allowedTopologyLinkTypes ?? [])
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        Guard.Against(
            AllowedTopologyLinkTypes.Any(LinkTypeRules.IsAdjacency),
            "Impacted-location expansion topology link types cannot include adjacency links.");
    }

    public bool IncludeSameLocation { get; }

    public bool IncludeSameRoom { get; }

    public int AdjacencyDepth { get; }

    public int TopologyDepth { get; }

    public IReadOnlyList<LinkType> AllowedTopologyLinkTypes { get; }

    public bool HasAdjacencyTraversal => AdjacencyDepth > 0;

    public bool HasTopologyTraversal => TopologyDepth > 0 && AllowedTopologyLinkTypes.Count > 0;

    public static ImpactedLocationExpansionSettings FromProfile(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ImpactedLocationExpansionSettings(
            includeSameLocation: profile.IncludeSameLocation,
            includeSameRoom: profile.IncludeSameRoom,
            adjacencyDepth: DiseaseTraceProfileSemantics.GetAdjacencyTraversalDepth(profile),
            topologyDepth: DiseaseTraceProfileSemantics.GetTopologyTraversalDepth(profile),
            allowedTopologyLinkTypes: DiseaseTraceProfileSemantics.GetAllowedTopologyLinkTypes(profile));
    }
}
