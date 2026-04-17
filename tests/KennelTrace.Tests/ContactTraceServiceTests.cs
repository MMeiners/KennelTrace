using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;
using KennelTrace.Infrastructure.Features.Tracing.ContactTracing;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class ContactTraceServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_ContactTrace_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public async Task InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(GetServerConnectionString())
        {
            InitialCatalog = _databaseName
        };

        _connectionString = builder.ConnectionString;

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Source_Animal_With_Multiple_Stays_Uses_All_Overlapping_Source_Stays_And_Excludes_The_Seed_Animal()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "CIV",
            name: "Canine influenza",
            now,
            includeSameLocation: true,
            includeSameRoom: false,
            includeAdjacent: false,
            adjacencyDepth: 0,
            includeTopologyLinks: false,
            topologyDepth: 0);

        var seedAnimal = await AddAnimalAsync(context, "SRC-001", "Source", now);
        var sameLocationOne = await AddAnimalAsync(context, "A-101", "Alpha", now);
        var sameLocationTwo = await AddAnimalAsync(context, "A-102", "Bravo", now);

        var sourceStayOne = await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 10, 0));
        var sourceStayTwo = await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 12, 0), Utc(2026, 4, 17, 14, 0));
        await AddStayAsync(context, sameLocationOne.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 9, 30));
        await AddStayAsync(context, sameLocationTwo.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 12, 30), Utc(2026, 4, 17, 13, 30));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 15, 0),
            sourceAnimalId: seedAnimal.AnimalId));

        Assert.Equal([sourceStayOne.MovementEventId, sourceStayTwo.MovementEventId], result.SourceStayIds);
        Assert.Equal(
            [layout.AdoptionKennelOne.LocationId, layout.AdoptionKennelTwo.LocationId],
            result.ImpactedLocations.Select(x => x.LocationId).ToArray());
        Assert.All(result.ImpactedLocations, x => Assert.Equal([TraceReasonCode.SameLocation], x.ReasonCodes));

        Assert.Equal(["A-101", "A-102"], result.ImpactedAnimals.Select(x => x.AnimalNumber.Value).ToArray());
        Assert.DoesNotContain(result.ImpactedAnimals, x => x.AnimalId == seedAnimal.AnimalId);
        Assert.Equal(
            [sourceStayOne.MovementEventId, sourceStayTwo.MovementEventId],
            result.ImpactedAnimals.SelectMany(x => x.OverlappingStays).Select(x => x.SourceStayId).Distinct().Order().ToArray());
    }

    [Fact]
    public async Task Explicit_Source_Stay_Uses_Only_The_Selected_Stay()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "PARVO",
            name: "Parvo",
            now,
            includeSameLocation: true,
            includeSameRoom: false,
            includeAdjacent: false,
            adjacencyDepth: 0,
            includeTopologyLinks: false,
            topologyDepth: 0);

        var seedAnimal = await AddAnimalAsync(context, "SRC-002", "Source", now);
        var sameLocationOne = await AddAnimalAsync(context, "A-201", "Charlie", now);
        var sameLocationTwo = await AddAnimalAsync(context, "A-202", "Delta", now);

        var sourceStayOne = await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 12, 0), Utc(2026, 4, 17, 14, 0));
        await AddStayAsync(context, sameLocationOne.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 9, 30));
        await AddStayAsync(context, sameLocationTwo.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 12, 30), Utc(2026, 4, 17, 13, 0));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 15, 0),
            sourceStayId: sourceStayOne.MovementEventId));

        Assert.Equal([sourceStayOne.MovementEventId], result.SourceStayIds);
        var impactedLocation = Assert.Single(result.ImpactedLocations);
        Assert.Equal(layout.AdoptionKennelOne.LocationId, impactedLocation.LocationId);

        var impactedAnimal = Assert.Single(result.ImpactedAnimals);
        Assert.Equal("A-201", impactedAnimal.AnimalNumber.Value);
        Assert.All(impactedAnimal.Reasons, x => Assert.Equal(sourceStayOne.MovementEventId, x.SourceStayId));
    }

    [Fact]
    public async Task Open_Source_Stay_Uses_The_Request_Window_End_For_Overlap()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "URI",
            name: "Upper respiratory",
            now,
            includeSameLocation: true,
            includeSameRoom: false,
            includeAdjacent: false,
            adjacencyDepth: 0,
            includeTopologyLinks: false,
            topologyDepth: 0);

        var seedAnimal = await AddAnimalAsync(context, "SRC-003", "Source", now);
        var exposedAnimal = await AddAnimalAsync(context, "A-301", "Echo", now);

        await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), endUtc: null);
        await AddStayAsync(context, exposedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 17, 0), Utc(2026, 4, 17, 19, 0));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 15, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 18, 0),
            sourceAnimalId: seedAnimal.AnimalId));

        var overlap = Assert.Single(Assert.Single(result.ImpactedAnimals).OverlappingStays);
        Assert.Null(overlap.SourceEndUtc);
        Assert.Equal(Utc(2026, 4, 17, 17, 0), overlap.OverlapStartUtc);
        Assert.Equal(Utc(2026, 4, 17, 18, 0), overlap.OverlapEndUtc);
    }

    [Fact]
    public async Task Trace_Uses_Same_Room_Adjacency_And_Allowed_Topology_Link_Types_From_Persisted_Data()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "DIST",
            name: "Distemper",
            now,
            includeSameLocation: true,
            includeSameRoom: true,
            includeAdjacent: true,
            adjacencyDepth: 1,
            includeTopologyLinks: true,
            topologyDepth: 1,
            topologyLinkTypes: [LinkType.Airflow]);

        var seedAnimal = await AddAnimalAsync(context, "SRC-004", "Source", now);
        var sameLocationAnimal = await AddAnimalAsync(context, "A-401", "Foxtrot", now);
        var adjacentAnimal = await AddAnimalAsync(context, "A-402", "Golf", now);
        var sameRoomAnimal = await AddAnimalAsync(context, "A-403", "Hotel", now);
        var airflowAnimal = await AddAnimalAsync(context, "A-404", "India", now);
        var filteredTopologyAnimal = await AddAnimalAsync(context, "A-405", "Juliet", now);

        await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 12, 0));
        await AddStayAsync(context, sameLocationAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, adjacentAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, sameRoomAnimal.AnimalId, layout.AdoptionKennelThree.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, airflowAnimal.AnimalId, layout.IsolationKennel.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, filteredTopologyAnimal.AnimalId, layout.MedicalKennel.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 13, 0),
            sourceAnimalId: seedAnimal.AnimalId));

        Assert.Equal(
            new[]
            {
                layout.AdoptionRoom.LocationId,
                layout.AdoptionKennelOne.LocationId,
                layout.AdoptionKennelTwo.LocationId,
                layout.AdoptionKennelThree.LocationId,
                layout.IsolationRoom.LocationId,
                layout.IsolationKennel.LocationId
            }.OrderBy(x => x).ToArray(),
            result.ImpactedLocations.Select(x => x.LocationId).ToArray());

        var sameRoomLocation = Assert.Single(result.ImpactedLocations, x => x.LocationId == layout.AdoptionKennelThree.LocationId);
        Assert.Equal(ImpactedLocationMatchKind.ScopedLocation, sameRoomLocation.MatchKind);
        Assert.Equal(layout.AdoptionRoom.LocationId, sameRoomLocation.ScopeLocationId);
        Assert.Equal([TraceReasonCode.SameRoom], sameRoomLocation.ReasonCodes);

        var airflowLocation = Assert.Single(result.ImpactedLocations, x => x.LocationId == layout.IsolationKennel.LocationId);
        Assert.Equal(ImpactedLocationMatchKind.ScopedLocation, airflowLocation.MatchKind);
        Assert.Equal(layout.IsolationRoom.LocationId, airflowLocation.ScopeLocationId);
        Assert.Equal([TraceReasonCode.AirflowLinked], airflowLocation.ReasonCodes);

        Assert.Equal(["A-401", "A-402", "A-403", "A-404"], result.ImpactedAnimals.Select(x => x.AnimalNumber.Value).ToArray());
        Assert.DoesNotContain(result.ImpactedAnimals, x => x.AnimalNumber.Value == "A-405");

        var adjacentResult = Assert.Single(result.ImpactedAnimals, x => x.AnimalNumber.Value == "A-402");
        Assert.Equal([TraceReasonCode.SameRoom, TraceReasonCode.Adjacent], adjacentResult.ReasonCodes);

        var airflowResult = Assert.Single(result.ImpactedAnimals, x => x.AnimalNumber.Value == "A-404");
        Assert.Equal([TraceReasonCode.AirflowLinked], airflowResult.ReasonCodes);
    }

    [Fact]
    public async Task Optional_Location_Scope_Narrows_Results_To_The_Selected_Location_And_Its_Descendants()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "BORD",
            name: "Bordetella",
            now,
            includeSameLocation: true,
            includeSameRoom: true,
            includeAdjacent: true,
            adjacencyDepth: 1,
            includeTopologyLinks: true,
            topologyDepth: 1,
            topologyLinkTypes: [LinkType.Airflow]);

        var seedAnimal = await AddAnimalAsync(context, "SRC-005", "Source", now);
        var sameRoomAnimal = await AddAnimalAsync(context, "A-501", "Kilo", now);
        var airflowAnimal = await AddAnimalAsync(context, "A-502", "Lima", now);

        var sourceStay = await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 12, 0));
        await AddStayAsync(context, sameRoomAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, airflowAnimal.AnimalId, layout.IsolationKennel.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 13, 0),
            sourceAnimalId: seedAnimal.AnimalId,
            locationScopeLocationId: layout.IsolationRoom.LocationId));

        Assert.Equal([sourceStay.MovementEventId], result.SourceStayIds);
        Assert.Equal(
            [layout.IsolationRoom.LocationId, layout.IsolationKennel.LocationId],
            result.ImpactedLocations.Select(x => x.LocationId).ToArray());
        Assert.Equal(["A-502"], result.ImpactedAnimals.Select(x => x.AnimalNumber.Value).ToArray());
        Assert.DoesNotContain(result.ImpactedLocations, x => x.LocationId == layout.AdoptionKennelOne.LocationId);
        Assert.DoesNotContain(result.ImpactedLocations, x => x.LocationId == layout.AdoptionKennelTwo.LocationId);
    }

    [Fact]
    public async Task Partial_Graph_Data_Still_Returns_What_Can_Be_Proven_From_Persisted_Relationships()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now, includeAdjacencyLinks: false);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "RING",
            name: "Ringworm",
            now,
            includeSameLocation: false,
            includeSameRoom: false,
            includeAdjacent: true,
            adjacencyDepth: 1,
            includeTopologyLinks: true,
            topologyDepth: 1,
            topologyLinkTypes: [LinkType.Connected]);

        var seedAnimal = await AddAnimalAsync(context, "SRC-006", "Source", now);
        var notProvableAnimal = await AddAnimalAsync(context, "A-601", "Mike", now);
        var connectedRoomAnimal = await AddAnimalAsync(context, "A-602", "November", now);

        await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 12, 0));
        await AddStayAsync(context, notProvableAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, connectedRoomAnimal.AnimalId, layout.MedicalRoom.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));

        var service = CreateService(context);

        var result = await service.RunAsync(new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 13, 0),
            sourceAnimalId: seedAnimal.AnimalId));

        Assert.Equal([layout.MedicalRoom.LocationId, layout.MedicalKennel.LocationId], result.ImpactedLocations.Select(x => x.LocationId).ToArray());
        Assert.Equal(["A-602"], result.ImpactedAnimals.Select(x => x.AnimalNumber.Value).ToArray());
        Assert.DoesNotContain(result.ImpactedAnimals, x => x.AnimalNumber.Value == "A-601");
    }

    [Fact]
    public async Task Results_Are_Deterministic_When_Run_Repeatedly_Against_The_Same_Persisted_Data()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 8, 0);
        var layout = await SeedPilotFacilityAsync(context, now);
        var profile = await AddProfileAsync(
            context,
            diseaseCode: "MIXED",
            name: "Mixed test disease",
            now,
            includeSameLocation: true,
            includeSameRoom: true,
            includeAdjacent: true,
            adjacencyDepth: 1,
            includeTopologyLinks: true,
            topologyDepth: 1,
            topologyLinkTypes: [LinkType.Airflow, LinkType.Connected]);

        var seedAnimal = await AddAnimalAsync(context, "SRC-007", "Source", now);
        var adjacentAnimal = await AddAnimalAsync(context, "A-701", "Zulu", now);
        var sameLocationAnimal = await AddAnimalAsync(context, "A-700", "Alpha", now);
        var connectedAnimal = await AddAnimalAsync(context, "A-702", "Beta", now);

        await AddStayAsync(context, connectedAnimal.AnimalId, layout.MedicalKennel.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, seedAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 8, 0), Utc(2026, 4, 17, 12, 0));
        await AddStayAsync(context, adjacentAnimal.AnimalId, layout.AdoptionKennelTwo.LocationId, Utc(2026, 4, 17, 9, 0), Utc(2026, 4, 17, 10, 0));
        await AddStayAsync(context, sameLocationAnimal.AnimalId, layout.AdoptionKennelOne.LocationId, Utc(2026, 4, 17, 9, 30), Utc(2026, 4, 17, 10, 30));

        var service = CreateService(context);
        var request = new ContactTraceRequest(
            profile.DiseaseTraceProfileId,
            traceWindowStartUtc: Utc(2026, 4, 17, 7, 0),
            traceWindowEndUtc: Utc(2026, 4, 17, 13, 0),
            sourceAnimalId: seedAnimal.AnimalId);

        var first = await service.RunAsync(request);
        var second = await service.RunAsync(request);

        Assert.Equal(first.SourceStayIds, second.SourceStayIds);
        Assert.Equal(
            first.ImpactedLocations.Select(FormatLocationResult).ToArray(),
            second.ImpactedLocations.Select(FormatLocationResult).ToArray());
        Assert.Equal(
            first.ImpactedAnimals.Select(FormatAnimalResult).ToArray(),
            second.ImpactedAnimals.Select(FormatAnimalResult).ToArray());
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static ContactTraceService CreateService(KennelTraceDbContext context) => new(context);

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }

    private static string FormatLocationResult(ImpactedLocationResult result) =>
        $"{result.LocationId}|{result.MatchKind}|{result.ScopeLocationId}|{result.TraversalDepth}|{result.ViaLinkType}|{string.Join(',', result.ReasonCodes)}";

    private static string FormatAnimalResult(ImpactedAnimalResult result) =>
        $"{result.AnimalNumber.Value}|{result.ImpactedLocationId}|{string.Join(',', result.ReasonCodes)}|{string.Join(';', result.OverlappingStays.Select(x => $"{x.SourceStayId}:{x.OverlappingStayId}:{x.OverlapStartUtc:O}:{x.OverlapEndUtc:O}"))}";

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private static async Task<PilotFacilityLayout> SeedPilotFacilityAsync(
        KennelTraceDbContext context,
        DateTime now,
        bool includeAdjacencyLinks = true)
    {
        var facility = new Facility(new FacilityCode($"PHX-{Guid.NewGuid():N}".Substring(0, 12)), "Phoenix Main", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var adoptionRoom = await AddLocationAsync(context, facility.FacilityId, $"ROOM-{Guid.NewGuid():N}".Substring(0, 12), "Adoption Room", LocationType.Room, now);
        var isolationRoom = await AddLocationAsync(context, facility.FacilityId, $"ISO-{Guid.NewGuid():N}".Substring(0, 12), "Isolation", LocationType.Isolation, now);
        var medicalRoom = await AddLocationAsync(context, facility.FacilityId, $"MED-{Guid.NewGuid():N}".Substring(0, 12), "Medical", LocationType.Medical, now);

        var adoptionKennelOne = await AddLocationAsync(context, facility.FacilityId, $"AK1-{Guid.NewGuid():N}".Substring(0, 12), "Adoption Kennel 1", LocationType.Kennel, now, adoptionRoom.LocationId, gridRow: 0, gridColumn: 0);
        var adoptionKennelTwo = await AddLocationAsync(context, facility.FacilityId, $"AK2-{Guid.NewGuid():N}".Substring(0, 12), "Adoption Kennel 2", LocationType.Kennel, now, adoptionRoom.LocationId, gridRow: 0, gridColumn: 1);
        var adoptionKennelThree = await AddLocationAsync(context, facility.FacilityId, $"AK3-{Guid.NewGuid():N}".Substring(0, 12), "Adoption Kennel 3", LocationType.Kennel, now, adoptionRoom.LocationId, gridRow: 0, gridColumn: 2);
        var isolationKennel = await AddLocationAsync(context, facility.FacilityId, $"IK1-{Guid.NewGuid():N}".Substring(0, 12), "Isolation Kennel", LocationType.Kennel, now, isolationRoom.LocationId, gridRow: 0, gridColumn: 0);
        var medicalKennel = await AddLocationAsync(context, facility.FacilityId, $"MK1-{Guid.NewGuid():N}".Substring(0, 12), "Medical Kennel", LocationType.Kennel, now, medicalRoom.LocationId, gridRow: 0, gridColumn: 0);

        if (includeAdjacencyLinks)
        {
            await AddLinkPairAsync(context, facility.FacilityId, adoptionKennelOne.LocationId, adoptionKennelTwo.LocationId, LinkType.AdjacentRight, now);
            await AddLinkPairAsync(context, facility.FacilityId, adoptionKennelTwo.LocationId, adoptionKennelThree.LocationId, LinkType.AdjacentRight, now);
        }

        await AddLinkPairAsync(context, facility.FacilityId, adoptionRoom.LocationId, isolationRoom.LocationId, LinkType.Airflow, now);
        await AddLinkPairAsync(context, facility.FacilityId, adoptionRoom.LocationId, medicalRoom.LocationId, LinkType.Connected, now);

        return new PilotFacilityLayout(
            facility,
            adoptionRoom,
            isolationRoom,
            medicalRoom,
            adoptionKennelOne,
            adoptionKennelTwo,
            adoptionKennelThree,
            isolationKennel,
            medicalKennel);
    }

    private static async Task<Location> AddLocationAsync(
        KennelTraceDbContext context,
        int facilityId,
        string locationCode,
        string name,
        LocationType locationType,
        DateTime now,
        int? parentLocationId = null,
        int? gridRow = null,
        int? gridColumn = null)
    {
        var location = new Location(
            facilityId,
            locationType,
            new LocationCode(locationCode),
            name,
            now,
            now,
            parentLocationId,
            gridRow: gridRow,
            gridColumn: gridColumn);

        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task AddLinkPairAsync(
        KennelTraceDbContext context,
        int facilityId,
        int fromLocationId,
        int toLocationId,
        LinkType forwardLinkType,
        DateTime now)
    {
        context.LocationLinks.AddRange(
            new LocationLink(facilityId, fromLocationId, toLocationId, forwardLinkType, now, now),
            new LocationLink(facilityId, toLocationId, fromLocationId, LinkTypeRules.InverseOf(forwardLinkType), now, now));
        await context.SaveChangesAsync();
    }

    private static async Task<Animal> AddAnimalAsync(KennelTraceDbContext context, string animalNumber, string name, DateTime now)
    {
        var animal = new Animal(new AnimalCode(animalNumber), now, now, name: name);
        context.Animals.Add(animal);
        await context.SaveChangesAsync();
        return animal;
    }

    private static async Task<MovementEvent> AddStayAsync(
        KennelTraceDbContext context,
        int animalId,
        int locationId,
        DateTime startUtc,
        DateTime? endUtc)
    {
        var stay = new MovementEvent(animalId, locationId, startUtc, startUtc, startUtc, endUtc);
        context.MovementEvents.Add(stay);
        await context.SaveChangesAsync();
        return stay;
    }

    private static async Task<DiseaseTraceProfile> AddProfileAsync(
        KennelTraceDbContext context,
        string diseaseCode,
        string name,
        DateTime now,
        bool includeSameLocation,
        bool includeSameRoom,
        bool includeAdjacent,
        int adjacencyDepth,
        bool includeTopologyLinks,
        int topologyDepth,
        IReadOnlyCollection<LinkType>? topologyLinkTypes = null)
    {
        var disease = new Disease(new DiseaseCode(diseaseCode), name, now, now);
        context.Diseases.Add(disease);
        await context.SaveChangesAsync();

        var profile = new DiseaseTraceProfile(
            disease.DiseaseId,
            defaultLookbackHours: 72,
            createdUtc: now,
            modifiedUtc: now,
            includeSameLocation: includeSameLocation,
            includeSameRoom: includeSameRoom,
            includeAdjacent: includeAdjacent,
            adjacencyDepth: adjacencyDepth,
            includeTopologyLinks: includeTopologyLinks,
            topologyDepth: topologyDepth);

        context.DiseaseTraceProfiles.Add(profile);
        await context.SaveChangesAsync();

        var allowedTopologyLinkTypes = topologyLinkTypes ?? [];
        if (allowedTopologyLinkTypes.Count > 0)
        {
            context.DiseaseTraceProfileTopologyLinkTypes.AddRange(allowedTopologyLinkTypes
                .Distinct()
                .Select(x => new DiseaseTraceProfileTopologyLinkType(profile.DiseaseTraceProfileId, x)));
            await context.SaveChangesAsync();
        }

        return profile;
    }

    private sealed record PilotFacilityLayout(
        Facility Facility,
        Location AdoptionRoom,
        Location IsolationRoom,
        Location MedicalRoom,
        Location AdoptionKennelOne,
        Location AdoptionKennelTwo,
        Location AdoptionKennelThree,
        Location IsolationKennel,
        Location MedicalKennel);
}
