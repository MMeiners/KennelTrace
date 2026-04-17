using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;

namespace KennelTrace.Domain.Features.Tracing;

public sealed class ImpactedLocationGraphExpander
{
    public IReadOnlyList<ExpandedImpactedLocation> Expand(ImpactedLocationExpansionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var locationIndex = request.Snapshot.Locations.ToDictionary(x => x.LocationId);
        var childrenByParent = request.Snapshot.Locations
            .Where(x => x.ParentLocationId is not null)
            .GroupBy(x => x.ParentLocationId!.Value)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.LocationId).Select(y => y.LocationId).ToArray());

        var linksByFrom = request.Snapshot.Links
            .GroupBy(x => x.FromLocationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(y => y.LinkType).ThenBy(y => y.ToLocationId).ToArray());

        var impactedLocations = new Dictionary<int, ReasonAccumulator>();

        foreach (var source in CreateSources(request))
        {
            if (request.Settings.IncludeSameLocation)
            {
                AddReason(
                    impactedLocations,
                    source.LocationId,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        source.LocationId,
                        source.SourceStayId));
            }

            if (request.Settings.IncludeSameRoom)
            {
                ExpandSameRoom(source, locationIndex, childrenByParent, impactedLocations);
            }

            if (request.Settings.HasAdjacencyTraversal)
            {
                ExpandAdjacency(source, request.Settings.AdjacencyDepth, linksByFrom, impactedLocations);
            }

            if (request.Settings.HasTopologyTraversal)
            {
                ExpandTopology(
                    source,
                    request.Settings,
                    locationIndex,
                    childrenByParent,
                    linksByFrom,
                    impactedLocations);
            }
        }

        return impactedLocations
            .OrderBy(x => x.Key)
            .Select(x => x.Value.Build())
            .ToArray();
    }

    private static IReadOnlyList<SourceContext> CreateSources(ImpactedLocationExpansionRequest request)
    {
        var staySources = request.SourceStays
            .Select(x => new SourceContext(x.LocationId, x.StayId))
            .ToArray();

        var locationsWithStaySources = staySources
            .Select(x => x.LocationId)
            .ToHashSet();

        var locationSources = request.SourceLocations
            .Where(x => !locationsWithStaySources.Contains(x.LocationId))
            .Select(x => new SourceContext(x.LocationId, null));

        return staySources
            .Concat(locationSources)
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.SourceStayId is null ? 1 : 0)
            .ThenBy(x => x.SourceStayId)
            .ToArray();
    }

    private static void ExpandSameRoom(
        SourceContext source,
        IReadOnlyDictionary<int, TraceGraphLocation> locationIndex,
        IReadOnlyDictionary<int, int[]> childrenByParent,
        IDictionary<int, ReasonAccumulator> impactedLocations)
    {
        var scopeLocationId = ResolveSameRoomScopeLocationId(source.LocationId, locationIndex);
        if (scopeLocationId is null)
        {
            return;
        }

        AddReason(
            impactedLocations,
            scopeLocationId.Value,
            new ImpactedLocationReason(
                TraceReasonCode.SameRoom,
                source.LocationId,
                source.SourceStayId));

        if (!childrenByParent.TryGetValue(scopeLocationId.Value, out var childLocationIds))
        {
            return;
        }

        foreach (var childLocationId in childLocationIds)
        {
            AddReason(
                impactedLocations,
                childLocationId,
                new ImpactedLocationReason(
                    TraceReasonCode.SameRoom,
                    source.LocationId,
                    source.SourceStayId,
                    ImpactedLocationMatchKind.ScopedLocation,
                    scopeLocationId: scopeLocationId.Value));
        }
    }

    private static void ExpandAdjacency(
        SourceContext source,
        int adjacencyDepth,
        IReadOnlyDictionary<int, TraceGraphLink[]> linksByFrom,
        IDictionary<int, ReasonAccumulator> impactedLocations)
    {
        var queue = new Queue<TraversalState>();
        queue.Enqueue(new TraversalState(source.LocationId, 0, null));

        var bestDepthByState = new Dictionary<(int LocationId, LinkType? LinkType), int>
        {
            [(source.LocationId, null)] = 0
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= adjacencyDepth || !linksByFrom.TryGetValue(current.LocationId, out var outgoingLinks))
            {
                continue;
            }

            foreach (var outgoingLink in outgoingLinks.Where(x => LinkTypeRules.IsAdjacency(x.LinkType)))
            {
                var nextDepth = current.Depth + 1;
                var nextStateKey = (outgoingLink.ToLocationId, (LinkType?)outgoingLink.LinkType);

                if (bestDepthByState.TryGetValue(nextStateKey, out var bestDepth) && bestDepth <= nextDepth)
                {
                    continue;
                }

                bestDepthByState[nextStateKey] = nextDepth;

                AddReason(
                    impactedLocations,
                    outgoingLink.ToLocationId,
                    new ImpactedLocationReason(
                        TraceReasonCode.Adjacent,
                        source.LocationId,
                        source.SourceStayId,
                        traversalDepth: nextDepth,
                        viaLinkType: outgoingLink.LinkType));

                queue.Enqueue(new TraversalState(outgoingLink.ToLocationId, nextDepth, outgoingLink.LinkType));
            }
        }
    }

    private static void ExpandTopology(
        SourceContext source,
        ImpactedLocationExpansionSettings settings,
        IReadOnlyDictionary<int, TraceGraphLocation> locationIndex,
        IReadOnlyDictionary<int, int[]> childrenByParent,
        IReadOnlyDictionary<int, TraceGraphLink[]> linksByFrom,
        IDictionary<int, ReasonAccumulator> impactedLocations)
    {
        foreach (var topologyLinkType in settings.AllowedTopologyLinkTypes)
        {
            // Keep each traversal inside one topology link family so reason metadata maps cleanly to the documented reason codes.
            foreach (var seedLocationId in ResolveTopologySeedLocationIds(source.LocationId, locationIndex))
            {
                ExpandTopologyFamily(
                    source,
                    seedLocationId,
                    topologyLinkType,
                    settings.TopologyDepth,
                    locationIndex,
                    childrenByParent,
                    linksByFrom,
                    impactedLocations);
            }
        }
    }

    private static void ExpandTopologyFamily(
        SourceContext source,
        int seedLocationId,
        LinkType topologyLinkType,
        int topologyDepth,
        IReadOnlyDictionary<int, TraceGraphLocation> locationIndex,
        IReadOnlyDictionary<int, int[]> childrenByParent,
        IReadOnlyDictionary<int, TraceGraphLink[]> linksByFrom,
        IDictionary<int, ReasonAccumulator> impactedLocations)
    {
        var queue = new Queue<(int LocationId, int Depth)>();
        queue.Enqueue((seedLocationId, 0));

        var bestDepthByLocation = new Dictionary<int, int>
        {
            [seedLocationId] = 0
        };

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Depth >= topologyDepth || !linksByFrom.TryGetValue(current.LocationId, out var outgoingLinks))
            {
                continue;
            }

            foreach (var outgoingLink in outgoingLinks.Where(x => x.LinkType == topologyLinkType))
            {
                var nextDepth = current.Depth + 1;

                if (bestDepthByLocation.TryGetValue(outgoingLink.ToLocationId, out var bestDepth) && bestDepth <= nextDepth)
                {
                    continue;
                }

                bestDepthByLocation[outgoingLink.ToLocationId] = nextDepth;

                var reasonCode = MapTopologyReasonCode(topologyLinkType);

                AddReason(
                    impactedLocations,
                    outgoingLink.ToLocationId,
                    new ImpactedLocationReason(
                        reasonCode,
                        source.LocationId,
                        source.SourceStayId,
                        traversalDepth: nextDepth,
                        viaLinkType: topologyLinkType));

                if (childrenByParent.TryGetValue(outgoingLink.ToLocationId, out var childLocationIds))
                {
                    foreach (var childLocationId in childLocationIds)
                    {
                        AddReason(
                            impactedLocations,
                            childLocationId,
                            new ImpactedLocationReason(
                                reasonCode,
                                source.LocationId,
                                source.SourceStayId,
                                ImpactedLocationMatchKind.ScopedLocation,
                                scopeLocationId: outgoingLink.ToLocationId,
                                traversalDepth: nextDepth,
                                viaLinkType: topologyLinkType));
                    }
                }

                queue.Enqueue((outgoingLink.ToLocationId, nextDepth));
            }
        }
    }

    private static IReadOnlyList<int> ResolveTopologySeedLocationIds(
        int sourceLocationId,
        IReadOnlyDictionary<int, TraceGraphLocation> locationIndex)
    {
        var seedLocationIds = new HashSet<int> { sourceLocationId };
        var sameRoomScopeLocationId = ResolveSameRoomScopeLocationId(sourceLocationId, locationIndex);

        if (sameRoomScopeLocationId is not null)
        {
            seedLocationIds.Add(sameRoomScopeLocationId.Value);
        }

        return seedLocationIds
            .OrderBy(x => x)
            .ToArray();
    }

    private static int? ResolveSameRoomScopeLocationId(
        int sourceLocationId,
        IReadOnlyDictionary<int, TraceGraphLocation> locationIndex)
    {
        if (!locationIndex.TryGetValue(sourceLocationId, out var sourceLocation))
        {
            return null;
        }

        if (LocationTypeRules.IsRoomLike(sourceLocation.LocationType))
        {
            return sourceLocation.LocationId;
        }

        if (sourceLocation.ParentLocationId is null)
        {
            return null;
        }

        if (locationIndex.TryGetValue(sourceLocation.ParentLocationId.Value, out var parentLocation))
        {
            return LocationTypeRules.IsRoomLike(parentLocation.LocationType)
                ? parentLocation.LocationId
                : null;
        }

        return sourceLocation.LocationType == LocationType.Kennel
            ? sourceLocation.ParentLocationId.Value
            : null;
    }

    private static TraceReasonCode MapTopologyReasonCode(LinkType linkType) =>
        linkType switch
        {
            LinkType.Airflow => TraceReasonCode.AirflowLinked,
            LinkType.TransportPath => TraceReasonCode.TransportPathLinked,
            LinkType.Connected => TraceReasonCode.ConnectedSpace,
            _ => throw new DomainValidationException($"Unsupported topology link type '{linkType}'.")
        };

    private static void AddReason(
        IDictionary<int, ReasonAccumulator> impactedLocations,
        int locationId,
        ImpactedLocationReason reason)
    {
        if (!impactedLocations.TryGetValue(locationId, out var accumulator))
        {
            accumulator = new ReasonAccumulator(locationId);
            impactedLocations.Add(locationId, accumulator);
        }

        accumulator.Add(reason);
    }

    private readonly record struct SourceContext(int LocationId, long? SourceStayId);

    private readonly record struct TraversalState(int LocationId, int Depth, LinkType? LastLinkType);

    private sealed class ReasonAccumulator
    {
        private readonly int _locationId;
        private readonly Dictionary<ReasonKey, ImpactedLocationReason> _reasons = [];

        public ReasonAccumulator(int locationId)
        {
            _locationId = locationId;
        }

        public void Add(ImpactedLocationReason reason)
        {
            var key = new ReasonKey(
                reason.ReasonCode,
                reason.SourceLocationId,
                reason.SourceStayId,
                reason.MatchKind,
                reason.ScopeLocationId,
                reason.ViaLinkType);

            if (_reasons.TryGetValue(key, out var existingReason) && existingReason.TraversalDepth <= reason.TraversalDepth)
            {
                return;
            }

            _reasons[key] = reason;
        }

        public ExpandedImpactedLocation Build() => new(_locationId, _reasons.Values);

        private readonly record struct ReasonKey(
            TraceReasonCode ReasonCode,
            int SourceLocationId,
            long? SourceStayId,
            ImpactedLocationMatchKind MatchKind,
            int? ScopeLocationId,
            LinkType? ViaLinkType);
    }
}
