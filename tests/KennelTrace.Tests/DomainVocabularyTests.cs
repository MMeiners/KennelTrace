using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Imports;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;

namespace KennelTrace.Tests;

public sealed class DomainVocabularyTests
{
    [Fact]
    public void FacilityCode_Rejects_Blank_Text()
    {
        Assert.Throws<DomainValidationException>(() => new FacilityCode(" "));
    }

    [Fact]
    public void Kennel_Cannot_Be_Created_Without_Parent()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new Location(
                facilityId: 1,
                locationType: LocationType.Kennel,
                locationCode: new LocationCode("KEN-1"),
                name: "Kennel 1",
                createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Kennels must have a valid parent room-like location.", exception.Message);
    }

    [Fact]
    public void Location_Rejects_Incomplete_Grid_Coordinates()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new Location(
                facilityId: 1,
                locationType: LocationType.Room,
                locationCode: new LocationCode("ROOM-1"),
                name: "Room 1",
                createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                gridRow: 1));

        Assert.Equal("GridRow and GridColumn must both be populated or both be null.", exception.Message);
    }

    [Fact]
    public void LocationLink_Rejects_Self_Links()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new LocationLink(
                facilityId: 1,
                fromLocationId: 10,
                toLocationId: 10,
                linkType: LinkType.Connected,
                createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc)));

        Assert.Equal("Self-links are invalid.", exception.Message);
    }

    [Fact]
    public void MovementEvent_Rejects_EndUtc_At_Or_Before_StartUtc()
    {
        var startUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<DomainValidationException>(() =>
            new MovementEvent(
                animalId: 1,
                locationId: 1,
                startUtc: startUtc,
                createdUtc: startUtc,
                modifiedUtc: startUtc,
                endUtc: startUtc));

        Assert.Equal("EndUtc must be greater than StartUtc.", exception.Message);
    }

    [Fact]
    public void MovementEvent_HalfOpen_Intervals_Do_Not_Overlap_On_Handoff()
    {
        var first = new MovementEvent(
            animalId: 1,
            locationId: 1,
            startUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));

        var second = new MovementEvent(
            animalId: 1,
            locationId: 2,
            startUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            createdUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            modifiedUtc: new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            endUtc: new DateTime(2026, 4, 1, 15, 0, 0, DateTimeKind.Utc));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void DiseaseTraceProfile_Rejects_Adjacency_LinkTypes()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new DiseaseTraceProfile(
                diseaseId: 1,
                defaultLookbackHours: 72,
                createdUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                modifiedUtc: new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
                includeTopologyLinks: true,
                topologyDepth: 1,
                topologyLinkTypes: [LinkType.AdjacentLeft, LinkType.Airflow]));

        Assert.Equal("DiseaseTraceProfile topology link types cannot include adjacency links.", exception.Message);
    }

    [Fact]
    public void Core_Entity_Shells_Can_Be_Constructed_With_Valid_Data()
    {
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var facility = new Facility(new FacilityCode("FAC-1"), "Main Shelter", "America/Phoenix", now, now);
        var room = new Location(1, LocationType.Room, new LocationCode("ROOM-1"), "Room 1", now, now);
        var kennel = new Location(1, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, parentLocationId: 1);
        var animal = new Animal(new AnimalCode("A-100"), now, now, name: "Scout");
        var disease = new Disease(new DiseaseCode("PARVO"), "Parvo", now, now);
        var profile = new DiseaseTraceProfile(
            diseaseId: 1,
            defaultLookbackHours: 48,
            createdUtc: now,
            modifiedUtc: now,
            includeTopologyLinks: true,
            topologyDepth: 2,
            topologyLinkTypes: [LinkType.Airflow, LinkType.Connected]);
        var batch = new ImportBatch("FacilityLayout", "pilot-layout.xlsx", ImportBatchRunMode.ValidateOnly, now);
        var issue = new ImportIssue(1, ImportIssueSeverity.Error, "Rooms", "Missing room code.", rowNumber: 7, itemKey: "ROOM-7");

        Assert.Equal("FAC-1", facility.FacilityCode.Value);
        Assert.Equal(LocationType.Kennel, kennel.LocationType);
        Assert.Equal("Scout", animal.Name);
        Assert.Equal(2, profile.TopologyLinkTypes.Count);
        Assert.Equal("pilot-layout.xlsx", batch.SourceFileName);
        Assert.Equal(7, issue.RowNumber);
        Assert.Equal("Room 1", room.Name);
        Assert.Equal("Parvo", disease.Name);
    }
}
