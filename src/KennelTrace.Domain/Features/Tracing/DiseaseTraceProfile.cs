using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class DiseaseTraceProfile
{
    private readonly List<DiseaseTraceProfileTopologyLinkType> _topologyLinkTypes = [];

    private DiseaseTraceProfile()
    {
    }

    public DiseaseTraceProfile(
        int diseaseId,
        int defaultLookbackHours,
        DateTime createdUtc,
        DateTime modifiedUtc,
        bool includeSameLocation = true,
        bool includeSameRoom = true,
        bool includeAdjacent = true,
        int adjacencyDepth = 1,
        bool includeTopologyLinks = false,
        int topologyDepth = 0,
        IEnumerable<LinkType>? topologyLinkTypes = null,
        bool isActive = true,
        string? notes = null)
    {
        Guard.Against(diseaseId <= 0, "diseaseId is required.");

        DiseaseId = diseaseId;
        DefaultLookbackHours = Guard.Positive(defaultLookbackHours, nameof(defaultLookbackHours));
        IncludeSameLocation = includeSameLocation;
        IncludeSameRoom = includeSameRoom;
        IncludeAdjacent = includeAdjacent;
        AdjacencyDepth = Guard.NonNegative(adjacencyDepth, nameof(adjacencyDepth));
        IncludeTopologyLinks = includeTopologyLinks;
        TopologyDepth = Guard.NonNegative(topologyDepth, nameof(topologyDepth));
        IsActive = isActive;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        CreatedUtc = Guard.RequiredUtc(createdUtc, nameof(createdUtc));
        ModifiedUtc = Guard.RequiredUtc(modifiedUtc, nameof(modifiedUtc));

        Guard.Against((!IncludeAdjacent && AdjacencyDepth != 0) || (IncludeAdjacent && AdjacencyDepth <= 0), "Adjacent settings must follow the documented IncludeAdjacent/AdjacencyDepth rule.");
        Guard.Against((!IncludeTopologyLinks && TopologyDepth != 0) || (IncludeTopologyLinks && TopologyDepth <= 0), "Topology settings must follow the documented IncludeTopologyLinks/TopologyDepth rule.");

        foreach (var linkType in topologyLinkTypes?.Distinct() ?? [])
        {
            _topologyLinkTypes.Add(new DiseaseTraceProfileTopologyLinkType(DiseaseTraceProfileId, linkType));
        }
    }

    public int DiseaseTraceProfileId { get; private set; }

    public int DiseaseId { get; private set; }

    public int DefaultLookbackHours { get; private set; }

    public bool IncludeSameLocation { get; private set; }

    public bool IncludeSameRoom { get; private set; }

    public bool IncludeAdjacent { get; private set; }

    public int AdjacencyDepth { get; private set; }

    public bool IncludeTopologyLinks { get; private set; }

    public int TopologyDepth { get; private set; }

    public bool IsActive { get; private set; }

    public string? Notes { get; private set; }

    public DateTime CreatedUtc { get; private set; }

    public DateTime ModifiedUtc { get; private set; }

    public IReadOnlyCollection<DiseaseTraceProfileTopologyLinkType> TopologyLinkTypes => _topologyLinkTypes;
}
