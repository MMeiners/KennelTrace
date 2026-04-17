using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Tests;

public sealed class ContactTraceContractsTests
{
    [Fact]
    public void ContactTraceRequest_Requires_Exactly_One_Source()
    {
        var startUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var endUtc = new DateTime(2026, 4, 12, 8, 0, 0, DateTimeKind.Utc);

        var noSource = Assert.Throws<DomainValidationException>(() =>
            new ContactTraceRequest(
                diseaseTraceProfileId: 1,
                traceWindowStartUtc: startUtc,
                traceWindowEndUtc: endUtc));

        var bothSources = Assert.Throws<DomainValidationException>(() =>
            new ContactTraceRequest(
                diseaseTraceProfileId: 1,
                traceWindowStartUtc: startUtc,
                traceWindowEndUtc: endUtc,
                sourceAnimalId: 10,
                sourceStayId: 25));

        Assert.Equal("Exactly one trace source must be specified: sourceAnimalId or sourceStayId.", noSource.Message);
        Assert.Equal("Exactly one trace source must be specified: sourceAnimalId or sourceStayId.", bothSources.Message);
    }

    [Fact]
    public void ContactTraceRequest_Requires_Explicit_Forward_Utc_Window()
    {
        var startUtc = new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc);
        var localEnd = new DateTime(2026, 4, 12, 8, 0, 0, DateTimeKind.Local);

        var kindException = Assert.Throws<DomainValidationException>(() =>
            new ContactTraceRequest(
                diseaseTraceProfileId: 1,
                traceWindowStartUtc: startUtc,
                traceWindowEndUtc: localEnd,
                sourceAnimalId: 10));

        var orderException = Assert.Throws<DomainValidationException>(() =>
            new ContactTraceRequest(
                diseaseTraceProfileId: 1,
                traceWindowStartUtc: startUtc,
                traceWindowEndUtc: startUtc,
                sourceAnimalId: 10));

        Assert.Equal("traceWindowEndUtc must use UTC.", kindException.Message);
        Assert.Equal("TraceWindowEndUtc must be greater than TraceWindowStartUtc.", orderException.Message);
    }

    [Fact]
    public void ContactTraceRequest_Supports_Source_Animal_With_Optional_Scope()
    {
        var request = new ContactTraceRequest(
            diseaseTraceProfileId: 7,
            traceWindowStartUtc: new DateTime(2026, 4, 10, 8, 0, 0, DateTimeKind.Utc),
            traceWindowEndUtc: new DateTime(2026, 4, 12, 8, 0, 0, DateTimeKind.Utc),
            sourceAnimalId: 14,
            facilityId: 2,
            locationScopeLocationId: 91);

        Assert.True(request.UsesSourceAnimal);
        Assert.False(request.UsesSourceStay);
        Assert.Equal(2, request.FacilityId);
        Assert.Equal(91, request.LocationScopeLocationId);
    }

    [Fact]
    public void Impacted_Results_Require_Reasons_And_Support_Scoped_Metadata()
    {
        var location = new ImpactedLocationResult(
            locationId: 100,
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 80,
            traversalDepth: 1,
            viaLinkType: LinkType.Airflow,
            reasonCodes: [TraceReasonCode.SameRoom, TraceReasonCode.AirflowLinked]);

        var animal = new ImpactedAnimalResult(
            animalId: 12,
            impactedLocationId: 100,
            overlappingStayIds: [2001, 2002],
            matchKind: ImpactedLocationMatchKind.ScopedLocation,
            scopeLocationId: 80,
            traversalDepth: 1,
            viaLinkType: LinkType.Airflow,
            reasonCodes: [TraceReasonCode.SameRoom, TraceReasonCode.AirflowLinked]);

        var missingReasons = Assert.Throws<DomainValidationException>(() =>
            new ImpactedLocationResult(locationId: 100, reasonCodes: []));

        Assert.Equal(ImpactedLocationMatchKind.ScopedLocation, location.MatchKind);
        Assert.Equal(80, location.ScopeLocationId);
        Assert.Equal(2, animal.OverlappingStayIds.Count);
        Assert.Equal("Impacted results must include at least one trace reason.", missingReasons.Message);
    }

    [Fact]
    public void ContactTraceResult_Normalizes_Source_Stays_And_Holds_Location_And_Animal_Results()
    {
        var result = new ContactTraceResult(
            diseaseTraceProfileId: 4,
            sourceStayIds: [300, 300, 301],
            impactedLocations:
            [
                new ImpactedLocationResult(
                    locationId: 11,
                    reasonCodes: [TraceReasonCode.SameLocation])
            ],
            impactedAnimals:
            [
                new ImpactedAnimalResult(
                    animalId: 22,
                    impactedLocationId: 11,
                    overlappingStayIds: [401],
                    reasonCodes: [TraceReasonCode.SameLocation])
            ]);

        Assert.Equal([300L, 301L], result.SourceStayIds);
        Assert.Single(result.ImpactedLocations);
        Assert.Single(result.ImpactedAnimals);
    }

    [Fact]
    public void DiseaseTraceProfileSemantics_Disable_Adjacency_When_Profile_Disables_It()
    {
        var profile = new DiseaseTraceProfile(
            diseaseId: 1,
            defaultLookbackHours: 72,
            createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            includeAdjacent: false,
            adjacencyDepth: 0,
            includeTopologyLinks: false,
            topologyDepth: 0);

        Assert.False(DiseaseTraceProfileSemantics.IsAdjacencyTraversalEnabled(profile));
        Assert.Equal(0, DiseaseTraceProfileSemantics.GetAdjacencyTraversalDepth(profile));
    }

    [Fact]
    public void DiseaseTraceProfileSemantics_Return_Only_Topology_Link_Types_When_Topology_Is_Enabled()
    {
        var disabledProfile = new DiseaseTraceProfile(
            diseaseId: 1,
            defaultLookbackHours: 72,
            createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            includeTopologyLinks: false,
            topologyDepth: 0,
            topologyLinkTypes: [LinkType.Airflow, LinkType.Connected]);

        var enabledProfile = new DiseaseTraceProfile(
            diseaseId: 1,
            defaultLookbackHours: 72,
            createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            includeTopologyLinks: true,
            topologyDepth: 2,
            topologyLinkTypes: [LinkType.Airflow, LinkType.Connected, LinkType.Airflow]);

        Assert.False(DiseaseTraceProfileSemantics.IsTopologyTraversalEnabled(disabledProfile));
        Assert.Empty(DiseaseTraceProfileSemantics.GetAllowedTopologyLinkTypes(disabledProfile));

        Assert.True(DiseaseTraceProfileSemantics.IsTopologyTraversalEnabled(enabledProfile));
        Assert.Equal(2, DiseaseTraceProfileSemantics.GetTopologyTraversalDepth(enabledProfile));
        Assert.Equal([LinkType.Airflow, LinkType.Connected], DiseaseTraceProfileSemantics.GetAllowedTopologyLinkTypes(enabledProfile));
        Assert.True(DiseaseTraceProfileSemantics.HasUsableTopologyTraversal(enabledProfile));
    }

    [Fact]
    public void DiseaseTraceProfileSemantics_Report_No_Usable_Topology_Traversal_Without_Allowed_Link_Types()
    {
        var profile = new DiseaseTraceProfile(
            diseaseId: 1,
            defaultLookbackHours: 72,
            createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            includeTopologyLinks: true,
            topologyDepth: 1);

        Assert.True(DiseaseTraceProfileSemantics.IsTopologyTraversalEnabled(profile));
        Assert.Empty(DiseaseTraceProfileSemantics.GetAllowedTopologyLinkTypes(profile));
        Assert.False(DiseaseTraceProfileSemantics.HasUsableTopologyTraversal(profile));
    }
}
