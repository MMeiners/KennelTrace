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
    public void Kennel_Requires_RoomLike_Parent()
    {
        var facilityId = Guid.NewGuid();
        var hallway = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Hallway,
            new LocationCode("HALL-1"),
            "Hallway 1");

        var exception = Assert.Throws<DomainValidationException>(() =>
            new Location(
                Guid.NewGuid(),
                facilityId,
                new FacilityCode("FAC-1"),
                LocationType.Kennel,
                new LocationCode("KEN-1"),
                "Kennel 1",
                hallway));

        Assert.Contains("cannot contain", exception.Message);
    }

    [Fact]
    public void Kennel_Cannot_Be_Created_Without_Parent()
    {
        var facilityId = Guid.NewGuid();

        var exception = Assert.Throws<DomainValidationException>(() =>
            new Location(
                Guid.NewGuid(),
                facilityId,
                new FacilityCode("FAC-1"),
                LocationType.Kennel,
                new LocationCode("KEN-1"),
                "Kennel 1"));

        Assert.Equal("Kennels must have a valid parent room-like location.", exception.Message);
    }

    [Fact]
    public void Location_Rejects_Negative_Grid_Row()
    {
        var facilityId = Guid.NewGuid();

        var exception = Assert.Throws<DomainValidationException>(() =>
            new Location(
                Guid.NewGuid(),
                facilityId,
                new FacilityCode("FAC-1"),
                LocationType.Room,
                new LocationCode("ROOM-1"),
                "Room 1",
                gridRow: -1));

        Assert.Equal("gridRow cannot be negative.", exception.Message);
    }

    [Fact]
    public void LocationLink_Rejects_CrossFacility_Links()
    {
        var left = new Location(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FacilityCode("FAC-1"),
            LocationType.Room,
            new LocationCode("ROOM-1"),
            "Room 1");

        var right = new Location(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new FacilityCode("FAC-2"),
            LocationType.Room,
            new LocationCode("ROOM-2"),
            "Room 2");

        var exception = Assert.Throws<DomainValidationException>(() =>
            LocationLink.Create(Guid.NewGuid(), left, right, LinkType.Connected));

        Assert.Equal("Cross-facility links are invalid in MVP.", exception.Message);
    }

    [Fact]
    public void LocationLink_Rejects_Adjacency_For_NonKennel_Endpoints()
    {
        var facilityId = Guid.NewGuid();
        var roomA = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Room,
            new LocationCode("ROOM-A"),
            "Room A");
        var roomB = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Room,
            new LocationCode("ROOM-B"),
            "Room B");

        var exception = Assert.Throws<DomainValidationException>(() =>
            LocationLink.Create(Guid.NewGuid(), roomA, roomB, LinkType.AdjacentLeft));

        Assert.Equal("Adjacency-style links should connect kennels.", exception.Message);
    }

    [Fact]
    public void LocationLink_Rejects_Topology_For_Kennel_Endpoints()
    {
        var facilityId = Guid.NewGuid();
        var room = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Room,
            new LocationCode("ROOM-1"),
            "Room 1");
        var kennelA = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Kennel,
            new LocationCode("KEN-A"),
            "Kennel A",
            room);
        var kennelB = new Location(
            Guid.NewGuid(),
            facilityId,
            new FacilityCode("FAC-1"),
            LocationType.Kennel,
            new LocationCode("KEN-B"),
            "Kennel B",
            room);

        var exception = Assert.Throws<DomainValidationException>(() =>
            LocationLink.Create(Guid.NewGuid(), kennelA, kennelB, LinkType.Connected));

        Assert.Equal("Topology-style links should connect room-like locations unless an intentional admin override is built.", exception.Message);
    }

    [Fact]
    public void LinkType_Inverse_Matches_Canonical_Pairs()
    {
        Assert.Equal(LinkType.AdjacentRight, LinkTypeRules.InverseOf(LinkType.AdjacentLeft));
        Assert.Equal(LinkType.AdjacentBelow, LinkTypeRules.InverseOf(LinkType.AdjacentAbove));
        Assert.Equal(LinkType.TransportPath, LinkTypeRules.InverseOf(LinkType.TransportPath));
    }

    [Fact]
    public void MovementEvent_Rejects_EndUtc_At_Or_Before_StartUtc()
    {
        var startUtc = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var exception = Assert.Throws<DomainValidationException>(() =>
            new MovementEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                startUtc,
                startUtc));

        Assert.Equal("EndUtc must be greater than StartUtc.", exception.Message);
    }

    [Fact]
    public void MovementEvent_HalfOpen_Intervals_Do_Not_Overlap_On_Handoff()
    {
        var first = new MovementEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));

        var second = new MovementEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 1, 15, 0, 0, DateTimeKind.Utc));

        Assert.False(first.Overlaps(second));
    }

    [Fact]
    public void Open_MovementEvent_Overlaps_Later_Interval()
    {
        var openStay = new MovementEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc));

        var laterStay = new MovementEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateTime(2026, 4, 2, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 2, 10, 0, 0, DateTimeKind.Utc));

        Assert.True(openStay.Overlaps(laterStay));
    }

    [Fact]
    public void DiseaseTraceProfile_Rejects_Adjacency_LinkTypes()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new DiseaseTraceProfile(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Parvo Default",
                72,
                includeSameLocation: true,
                includeSameRoom: true,
                includeAdjacentKennels: true,
                adjacencyDepth: 1,
                includedLinkTypes: [LinkType.AdjacentLeft, LinkType.Airflow],
                topologyDepth: 1));

        Assert.Equal("DiseaseTraceProfile topology link types cannot include adjacency links.", exception.Message);
    }

    [Fact]
    public void ImportIssue_Requires_Positive_Row_Number()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            new ImportIssue(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "layout.xlsx",
                "Rooms",
                0,
                "Missing room code.",
                isError: true));

        Assert.Equal("rowNumber must be greater than zero.", exception.Message);
    }

    [Fact]
    public void Core_Entity_Shells_Can_Be_Constructed_With_Valid_Data()
    {
        var facility = new Facility(Guid.NewGuid(), new FacilityCode("FAC-1"), "Main Shelter");
        var room = new Location(
            Guid.NewGuid(),
            facility.Id,
            facility.Code,
            LocationType.Room,
            new LocationCode("ROOM-1"),
            "Room 1");
        var kennel = new Location(
            Guid.NewGuid(),
            facility.Id,
            facility.Code,
            LocationType.Kennel,
            new LocationCode("KEN-1"),
            "Kennel 1",
            room);
        var animal = new Animal(Guid.NewGuid(), new AnimalCode("A-100"), "Scout");
        var disease = new Disease(Guid.NewGuid(), new DiseaseCode("PARVO"), "Parvo");
        var profile = new DiseaseTraceProfile(
            Guid.NewGuid(),
            disease.Id,
            "Default",
            48,
            includeSameLocation: true,
            includeSameRoom: true,
            includeAdjacentKennels: true,
            adjacencyDepth: 1,
            includedLinkTypes: [LinkType.Airflow, LinkType.Connected],
            topologyDepth: 2);
        var batch = new ImportBatch(
            Guid.NewGuid(),
            facility.Id,
            "pilot-layout.xlsx",
            new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc),
            isValidationOnly: true);

        Assert.Equal(LocationType.Kennel, kennel.LocationType);
        Assert.Equal("Scout", animal.DisplayName);
        Assert.Equal(2, profile.IncludedLinkTypes.Count);
        Assert.Equal("pilot-layout.xlsx", batch.SourceName);
    }
}
