using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class DiseaseTraceProfileTopologyLinkType
{
    private DiseaseTraceProfileTopologyLinkType()
    {
    }

    public DiseaseTraceProfileTopologyLinkType(int diseaseTraceProfileId, LinkType linkType)
    {
        Guard.Against(LinkTypeRules.IsAdjacency(linkType), "DiseaseTraceProfile topology link types cannot include adjacency links.");

        DiseaseTraceProfileId = diseaseTraceProfileId;
        LinkType = linkType;
    }

    public int DiseaseTraceProfileId { get; private set; }

    public LinkType LinkType { get; private set; }
}
