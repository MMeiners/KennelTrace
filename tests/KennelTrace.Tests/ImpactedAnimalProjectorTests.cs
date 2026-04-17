using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Tests;

public sealed class ImpactedAnimalProjectorTests
{
    private static readonly ImpactedAnimalProjector Sut = new();

    [Fact]
    public void Project_Returns_Overlapping_Same_Location_Exposure()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 12))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    101,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        sourceLocationId: 101,
                        sourceStayId: 5001))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 201, "A-201", 101, Utc(10, 9), Utc(10, 10), "Biscuit")
            ]));

        var result = Assert.Single(results);
        Assert.Equal(201, result.AnimalId);
        Assert.Equal("A-201", result.AnimalNumber.Value);
        Assert.Equal("Biscuit", result.AnimalName);
        Assert.Equal(101, result.ImpactedLocationId);
        Assert.Equal([TraceReasonCode.SameLocation], result.ReasonCodes);

        var overlap = Assert.Single(result.OverlappingStays);
        Assert.Equal(7001, overlap.OverlappingStayId);
        Assert.Equal(5001, overlap.SourceStayId);
        Assert.Equal(Utc(10, 9), overlap.OverlapStartUtc);
        Assert.Equal(Utc(10, 10), overlap.OverlapEndUtc);
    }

    [Fact]
    public void Project_Returns_Same_Room_Exposure_For_Different_Child_Locations()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 12))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    102,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameRoom,
                        sourceLocationId: 101,
                        sourceStayId: 5001,
                        matchKind: ImpactedLocationMatchKind.ScopedLocation,
                        scopeLocationId: 100))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 202, "A-202", 102, Utc(10, 9), Utc(10, 11), "Milo")
            ]));

        var result = Assert.Single(results);
        var reason = Assert.Single(result.Reasons);

        Assert.Equal(102, result.ImpactedLocationId);
        Assert.Equal(TraceReasonCode.SameRoom, reason.ReasonCode);
        Assert.Equal(ImpactedLocationMatchKind.ScopedLocation, reason.MatchKind);
        Assert.Equal(100, reason.ScopeLocationId);
    }

    [Fact]
    public void Project_Uses_Open_Source_Stay_With_Explicit_Trace_Window()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 15),
            traceWindowEndUtc: Utc(10, 20),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), endUtc: null)
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    101,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        sourceLocationId: 101,
                        sourceStayId: 5001))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 203, "A-203", 101, Utc(10, 18), endUtc: null, animalName: "Nova")
            ]));

        var overlap = Assert.Single(Assert.Single(results).OverlappingStays);
        Assert.Null(overlap.SourceEndUtc);
        Assert.Null(overlap.StayEndUtc);
        Assert.Equal(Utc(10, 18), overlap.OverlapStartUtc);
        Assert.Equal(Utc(10, 20), overlap.OverlapEndUtc);
    }

    [Fact]
    public void Project_Handles_Multiple_Source_Stays_Within_The_Selected_Window()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 10)),
                SourceStay(5002, 102, Utc(10, 12), Utc(10, 14))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    301,
                    new ImpactedLocationReason(
                        TraceReasonCode.ConnectedSpace,
                        sourceLocationId: 101,
                        sourceStayId: 5001,
                        traversalDepth: 1,
                        viaLinkType: LinkType.Connected),
                    new ImpactedLocationReason(
                        TraceReasonCode.AirflowLinked,
                        sourceLocationId: 102,
                        sourceStayId: 5002,
                        traversalDepth: 1,
                        viaLinkType: LinkType.Airflow))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 204, "A-204", 301, Utc(10, 9), Utc(10, 13), "Scout")
            ]));

        var result = Assert.Single(results);
        Assert.Equal([TraceReasonCode.AirflowLinked, TraceReasonCode.ConnectedSpace], result.ReasonCodes);
        Assert.Equal([7001L], result.OverlappingStayIds);
        Assert.Equal([5001L, 5002L], result.OverlappingStays.Select(x => x.SourceStayId).ToArray());
        Assert.Equal([Utc(10, 9), Utc(10, 12)], result.OverlappingStays.Select(x => x.OverlapStartUtc).ToArray());
        Assert.Equal([Utc(10, 10), Utc(10, 13)], result.OverlappingStays.Select(x => x.OverlapEndUtc).ToArray());
    }

    [Fact]
    public void Project_Filters_Reasons_To_Source_Stays_That_Actually_Overlap()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 10)),
                SourceStay(5002, 101, Utc(10, 12), Utc(10, 14))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    101,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        sourceLocationId: 101,
                        sourceStayId: 5001),
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        sourceLocationId: 101,
                        sourceStayId: 5002))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 205, "A-205", 101, Utc(10, 12, 30), Utc(10, 13), "Wren")
            ]));

        var result = Assert.Single(results);
        var reason = Assert.Single(result.Reasons);
        var overlap = Assert.Single(result.OverlappingStays);

        Assert.Equal(5002, reason.SourceStayId);
        Assert.Equal(5002, overlap.SourceStayId);
        Assert.Equal(Utc(10, 12, 30), overlap.OverlapStartUtc);
        Assert.Equal(Utc(10, 13), overlap.OverlapEndUtc);
    }

    [Fact]
    public void Project_Does_Not_Count_Same_Timestamp_Handoff_As_Overlap()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 12))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    101,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameLocation,
                        sourceLocationId: 101,
                        sourceStayId: 5001))
            ],
            candidateMovementStays:
            [
                CandidateStay(7001, 206, "A-206", 101, Utc(10, 12), Utc(10, 14), "Piper")
            ]));

        Assert.Empty(results);
    }

    [Fact]
    public void Project_Returns_Deterministic_Grouping_And_Order_When_Animals_Overlap_Through_Multiple_Locations_Or_Reasons()
    {
        var results = Sut.Project(new ImpactedAnimalProjectionRequest(
            traceWindowStartUtc: Utc(10, 0),
            traceWindowEndUtc: Utc(10, 18),
            sourceStayIntervals:
            [
                SourceStay(5001, 101, Utc(10, 8), Utc(10, 12))
            ],
            impactedLocations:
            [
                ImpactedLocation(
                    102,
                    new ImpactedLocationReason(
                        TraceReasonCode.SameRoom,
                        sourceLocationId: 101,
                        sourceStayId: 5001,
                        matchKind: ImpactedLocationMatchKind.ScopedLocation,
                        scopeLocationId: 100),
                    new ImpactedLocationReason(
                        TraceReasonCode.Adjacent,
                        sourceLocationId: 101,
                        sourceStayId: 5001,
                        traversalDepth: 1,
                        viaLinkType: LinkType.AdjacentRight)),
                ImpactedLocation(
                    201,
                    new ImpactedLocationReason(
                        TraceReasonCode.ConnectedSpace,
                        sourceLocationId: 101,
                        sourceStayId: 5001,
                        traversalDepth: 1,
                        viaLinkType: LinkType.Connected))
            ],
            candidateMovementStays:
            [
                CandidateStay(7002, 301, "A-100", 201, Utc(10, 9), Utc(10, 10), "Zulu"),
                CandidateStay(7001, 301, "A-100", 102, Utc(10, 9), Utc(10, 10), "Zulu"),
                CandidateStay(7004, 301, "A-100", 102, Utc(10, 10), Utc(10, 11), "Zulu"),
                CandidateStay(7003, 302, "A-050", 102, Utc(10, 9), Utc(10, 10), "Alpha")
            ]));

        Assert.Equal(
            [("A-050", 102), ("A-100", 102), ("A-100", 201)],
            results.Select(x => (x.AnimalNumber.Value, x.ImpactedLocationId)).ToArray());

        var groupedLocation = results[1];
        Assert.Equal([7001L, 7004L], groupedLocation.OverlappingStayIds);
        Assert.Equal([TraceReasonCode.SameRoom, TraceReasonCode.Adjacent], groupedLocation.ReasonCodes);
        Assert.Equal(
            [TraceReasonCode.SameRoom, TraceReasonCode.Adjacent],
            groupedLocation.Reasons.Select(x => x.ReasonCode).ToArray());
    }

    private static DateTime Utc(int day, int hour, int minute = 0) =>
        new(2026, 4, day, hour, minute, 0, DateTimeKind.Utc);

    private static ResolvedTraceSourceStayInterval SourceStay(
        long stayId,
        int locationId,
        DateTime startUtc,
        DateTime? endUtc = null) =>
        new(stayId, locationId, startUtc, endUtc);

    private static TraceCandidateMovementStay CandidateStay(
        long stayId,
        int animalId,
        string animalNumber,
        int locationId,
        DateTime startUtc,
        DateTime? endUtc = null,
        string? animalName = null) =>
        new(
            stayId,
            animalId,
            new AnimalCode(animalNumber),
            locationId,
            startUtc,
            endUtc,
            animalName);

    private static ExpandedImpactedLocation ImpactedLocation(
        int locationId,
        params ImpactedLocationReason[] reasons) =>
        new(locationId, reasons);
}
