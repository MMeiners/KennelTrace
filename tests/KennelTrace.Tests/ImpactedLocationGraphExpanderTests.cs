using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Tests;

public sealed class ImpactedLocationGraphExpanderTests
{
    private static readonly ImpactedLocationGraphExpander Sut = new();

    [Fact]
    public void Expand_Includes_Same_Location_For_Source_Stays_And_Additional_Source_Locations()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(includeSameLocation: true),
            snapshot: new TraceGraphSnapshot(),
            sourceStays: [new ResolvedTraceSourceStay(5001, 101)],
            sourceLocations: [new ResolvedTraceSourceLocation(101), new ResolvedTraceSourceLocation(102)]));

        Assert.Collection(
            results,
            location101 =>
            {
                Assert.Equal(101, location101.LocationId);
                Assert.Equal([TraceReasonCode.SameLocation], location101.ReasonCodes);

                var reason = Assert.Single(location101.Reasons);
                Assert.Equal(TraceReasonCode.SameLocation, reason.ReasonCode);
                Assert.Equal(101, reason.SourceLocationId);
                Assert.Equal(5001, reason.SourceStayId);
                Assert.Equal(ImpactedLocationMatchKind.ExactLocation, reason.MatchKind);
                Assert.Equal(0, reason.TraversalDepth);
                Assert.Null(reason.ViaLinkType);
            },
            location102 =>
            {
                Assert.Equal(102, location102.LocationId);
                Assert.Equal([TraceReasonCode.SameLocation], location102.ReasonCodes);

                var reason = Assert.Single(location102.Reasons);
                Assert.Equal(TraceReasonCode.SameLocation, reason.ReasonCode);
                Assert.Equal(102, reason.SourceLocationId);
                Assert.Null(reason.SourceStayId);
                Assert.Equal(ImpactedLocationMatchKind.ExactLocation, reason.MatchKind);
            });
    }

    [Fact]
    public void Expand_Derives_Same_Room_From_Containment_Instead_Of_Stored_Links()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: true),
            snapshot: new TraceGraphSnapshot(
                locations:
                [
                    Room(100),
                    Kennel(101, 100, gridRow: 0, gridColumn: 0),
                    Kennel(102, 100, gridRow: 4, gridColumn: 7)
                ]),
            sourceStays: [new ResolvedTraceSourceStay(5001, 101)]));

        Assert.Equal([100, 101, 102], results.Select(x => x.LocationId).ToArray());

        AssertReason(
            results[0],
            TraceReasonCode.SameRoom,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ExactLocation);

        AssertReason(
            results[1],
            TraceReasonCode.SameRoom,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 100);

        AssertReason(
            results[2],
            TraceReasonCode.SameRoom,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 100);
    }

    [Fact]
    public void Expand_Uses_Adjacency_Depth_One_Versus_Two()
    {
        var snapshot = new TraceGraphSnapshot(
            locations:
            [
                Kennel(101, 100),
                Kennel(102, 100),
                Kennel(103, 100)
            ],
            links:
            [
                Link(101, 102, LinkType.AdjacentRight),
                Link(102, 103, LinkType.AdjacentRight)
            ]);

        var depthOne = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                adjacencyDepth: 1),
            snapshot: snapshot,
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        var depthTwo = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                adjacencyDepth: 2),
            snapshot: snapshot,
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        Assert.Equal([102], depthOne.Select(x => x.LocationId).ToArray());
        Assert.Equal([102, 103], depthTwo.Select(x => x.LocationId).ToArray());

        AssertReason(
            depthOne[0],
            TraceReasonCode.Adjacent,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 1,
            viaLinkType: LinkType.AdjacentRight);

        AssertReason(
            depthTwo[1],
            TraceReasonCode.Adjacent,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 2,
            viaLinkType: LinkType.AdjacentRight);
    }

    [Fact]
    public void Expand_Uses_Directed_Links_As_Authored()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                adjacencyDepth: 1),
            snapshot: new TraceGraphSnapshot(
                locations:
                [
                    Kennel(101, 100),
                    Kennel(102, 100)
                ],
                links:
                [
                    Link(102, 101, LinkType.AdjacentLeft)
                ]),
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        Assert.Empty(results);
    }

    [Fact]
    public void Expand_Traces_Topology_Only_Across_Allowed_Link_Types_And_Maps_Reason_Codes()
    {
        var snapshot = new TraceGraphSnapshot(
            locations:
            [
                Room(100),
                Kennel(101, 100),
                Room(200),
                Kennel(201, 200),
                Isolation(300),
                Kennel(301, 300),
                Medical(400),
                Kennel(401, 400)
            ],
            links:
            [
                Link(100, 200, LinkType.Connected),
                Link(100, 300, LinkType.Airflow),
                Link(100, 400, LinkType.TransportPath)
            ]);

        var connectedAndAirflow = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                topologyDepth: 1,
                allowedTopologyLinkTypes: [LinkType.Airflow, LinkType.Connected]),
            snapshot: snapshot,
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        var transportOnly = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                topologyDepth: 1,
                allowedTopologyLinkTypes: [LinkType.TransportPath]),
            snapshot: snapshot,
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        Assert.Equal([200, 201, 300, 301], connectedAndAirflow.Select(x => x.LocationId).ToArray());

        AssertReason(
            connectedAndAirflow[0],
            TraceReasonCode.ConnectedSpace,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 1,
            viaLinkType: LinkType.Connected);

        AssertReason(
            connectedAndAirflow[1],
            TraceReasonCode.ConnectedSpace,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 200,
            traversalDepth: 1,
            viaLinkType: LinkType.Connected);

        AssertReason(
            connectedAndAirflow[2],
            TraceReasonCode.AirflowLinked,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 1,
            viaLinkType: LinkType.Airflow);

        AssertReason(
            connectedAndAirflow[3],
            TraceReasonCode.AirflowLinked,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 300,
            traversalDepth: 1,
            viaLinkType: LinkType.Airflow);

        Assert.Equal([400, 401], transportOnly.Select(x => x.LocationId).ToArray());

        AssertReason(
            transportOnly[0],
            TraceReasonCode.TransportPathLinked,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 1,
            viaLinkType: LinkType.TransportPath);

        AssertReason(
            transportOnly[1],
            TraceReasonCode.TransportPathLinked,
            sourceLocationId: 101,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 400,
            traversalDepth: 1,
            viaLinkType: LinkType.TransportPath);
    }

    [Fact]
    public void Expand_Returns_Only_What_Partial_Graph_Data_Can_Prove()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: true,
                topologyDepth: 1,
                allowedTopologyLinkTypes: [LinkType.Connected]),
            snapshot: new TraceGraphSnapshot(
                locations:
                [
                    Kennel(101, 100)
                ],
                links:
                [
                    Link(100, 200, LinkType.Connected)
                ]),
            sourceStays: [new ResolvedTraceSourceStay(5001, 101)]));

        Assert.Equal([100, 101, 200], results.Select(x => x.LocationId).ToArray());

        AssertReason(
            results[0],
            TraceReasonCode.SameRoom,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ExactLocation);

        AssertReason(
            results[1],
            TraceReasonCode.SameRoom,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 100);

        AssertReason(
            results[2],
            TraceReasonCode.ConnectedSpace,
            sourceLocationId: 101,
            sourceStayId: 5001,
            matchKind: ImpactedLocationMatchKind.ExactLocation,
            traversalDepth: 1,
            viaLinkType: LinkType.Connected);
    }

    [Fact]
    public void Expand_Does_Not_Infer_Adjacency_From_Irregular_Grid_Coordinates()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: false,
                adjacencyDepth: 1),
            snapshot: new TraceGraphSnapshot(
                locations:
                [
                    Room(100),
                    Kennel(101, 100, gridRow: 0, gridColumn: 0),
                    Kennel(102, 100, gridRow: 0, gridColumn: 1),
                    Kennel(103, 100, gridRow: 3, gridColumn: 0)
                ]),
            sourceLocations: [new ResolvedTraceSourceLocation(101)]));

        Assert.Empty(results);
    }

    [Fact]
    public void Expand_Returns_Deterministic_Output_When_Multiple_Reasons_Reach_The_Same_Location()
    {
        var results = Sut.Expand(new ImpactedLocationExpansionRequest(
            settings: new ImpactedLocationExpansionSettings(
                includeSameLocation: false,
                includeSameRoom: true,
                adjacencyDepth: 1,
                topologyDepth: 1,
                allowedTopologyLinkTypes: [LinkType.Connected, LinkType.Airflow]),
            snapshot: new TraceGraphSnapshot(
                locations:
                [
                    Room(100),
                    Kennel(101, 100),
                    Kennel(102, 100),
                    Isolation(200),
                    Kennel(201, 200)
                ],
                links:
                [
                    Link(101, 102, LinkType.AdjacentRight),
                    Link(100, 200, LinkType.Connected),
                    Link(100, 200, LinkType.Airflow)
                ]),
            sourceStays: [new ResolvedTraceSourceStay(5001, 101)]));

        Assert.Equal([100, 101, 102, 200, 201], results.Select(x => x.LocationId).ToArray());

        Assert.Equal([TraceReasonCode.SameRoom], results[0].ReasonCodes);
        Assert.Equal([TraceReasonCode.SameRoom], results[1].ReasonCodes);
        Assert.Equal([TraceReasonCode.SameRoom, TraceReasonCode.Adjacent], results[2].ReasonCodes);
        Assert.Equal([TraceReasonCode.AirflowLinked, TraceReasonCode.ConnectedSpace], results[3].ReasonCodes);
        Assert.Equal([TraceReasonCode.AirflowLinked, TraceReasonCode.ConnectedSpace], results[4].ReasonCodes);

        Assert.Equal(
            [TraceReasonCode.SameRoom, TraceReasonCode.Adjacent],
            results[2].Reasons.Select(x => x.ReasonCode).ToArray());

        Assert.Equal(
            [TraceReasonCode.AirflowLinked, TraceReasonCode.ConnectedSpace],
            results[3].Reasons.Select(x => x.ReasonCode).ToArray());

        Assert.Equal(
            [TraceReasonCode.AirflowLinked, TraceReasonCode.ConnectedSpace],
            results[4].Reasons.Select(x => x.ReasonCode).ToArray());
    }

    private static void AssertReason(
        ExpandedImpactedLocation location,
        TraceReasonCode reasonCode,
        int sourceLocationId,
        long? sourceStayId = null,
        ImpactedLocationMatchKind matchKind = ImpactedLocationMatchKind.ExactLocation,
        int? scopeLocationId = null,
        int traversalDepth = 0,
        LinkType? viaLinkType = null)
    {
        var reason = Assert.Single(location.Reasons, x =>
            x.ReasonCode == reasonCode
            && x.SourceLocationId == sourceLocationId
            && x.SourceStayId == sourceStayId
            && x.MatchKind == matchKind
            && x.ScopeLocationId == scopeLocationId
            && x.TraversalDepth == traversalDepth
            && x.ViaLinkType == viaLinkType);

        Assert.Equal(reasonCode, reason.ReasonCode);
    }

    private static TraceGraphLocation Room(int locationId) =>
        new(locationId, LocationType.Room);

    private static TraceGraphLocation Isolation(int locationId) =>
        new(locationId, LocationType.Isolation);

    private static TraceGraphLocation Medical(int locationId) =>
        new(locationId, LocationType.Medical);

    private static TraceGraphLocation Kennel(
        int locationId,
        int parentLocationId,
        int? gridRow = null,
        int? gridColumn = null) =>
        new(
            locationId,
            LocationType.Kennel,
            parentLocationId: parentLocationId,
            gridRow: gridRow,
            gridColumn: gridColumn);

    private static TraceGraphLink Link(int fromLocationId, int toLocationId, LinkType linkType) =>
        new(fromLocationId, toLocationId, linkType);
}
