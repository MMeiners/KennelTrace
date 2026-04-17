using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public static class DiseaseTraceProfileSemantics
{
    public static bool IsAdjacencyTraversalEnabled(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.IncludeAdjacent && profile.AdjacencyDepth > 0;
    }

    public static int GetAdjacencyTraversalDepth(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return IsAdjacencyTraversalEnabled(profile) ? profile.AdjacencyDepth : 0;
    }

    public static bool IsTopologyTraversalEnabled(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return profile.IncludeTopologyLinks && profile.TopologyDepth > 0;
    }

    public static int GetTopologyTraversalDepth(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return IsTopologyTraversalEnabled(profile) ? profile.TopologyDepth : 0;
    }

    public static IReadOnlyList<LinkType> GetAllowedTopologyLinkTypes(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (!IsTopologyTraversalEnabled(profile))
        {
            return [];
        }

        return profile.TopologyLinkTypes
            .Select(x => x.LinkType)
            .Distinct()
            .ToArray();
    }

    public static bool HasUsableTopologyTraversal(DiseaseTraceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return IsTopologyTraversalEnabled(profile) && GetAllowedTopologyLinkTypes(profile).Count > 0;
    }
}
