using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class FacilityMapReadServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_FacilityMap_{Guid.NewGuid():N}";
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
    public async Task Facilities_Are_Returned_Correctly()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 2, 8);

        context.Facilities.AddRange(
            new Facility(new FacilityCode("ALPHA"), "Alpha Shelter", "America/Phoenix", now, now),
            new Facility(new FacilityCode("BETA"), "Beta Shelter", "America/Phoenix", now, now, isActive: false));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var facilities = await service.ListFacilitiesAsync();

        Assert.Equal(2, facilities.Count);
        Assert.Collection(
            facilities,
            facility =>
            {
                Assert.Equal("ALPHA", facility.FacilityCode.Value);
                Assert.Equal("Alpha Shelter", facility.Name);
                Assert.True(facility.IsActive);
            },
            facility =>
            {
                Assert.Equal("BETA", facility.FacilityCode.Value);
                Assert.Equal("Beta Shelter", facility.Name);
                Assert.False(facility.IsActive);
            });
    }

    [Fact]
    public async Task Rooms_Are_Filtered_By_Facility()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 2, 8);

        var primaryFacility = new Facility(new FacilityCode("PHX"), "Phoenix", "America/Phoenix", now, now);
        var otherFacility = new Facility(new FacilityCode("TUC"), "Tucson", "America/Phoenix", now, now);
        context.Facilities.AddRange(primaryFacility, otherFacility);
        await context.SaveChangesAsync();

        var room = new Location(primaryFacility.FacilityId, LocationType.Room, new LocationCode("ROOM-A"), "Room A", now, now, displayOrder: 2);
        var medical = new Location(primaryFacility.FacilityId, LocationType.Medical, new LocationCode("MED-A"), "Medical A", now, now, displayOrder: 1);
        var otherRoom = new Location(otherFacility.FacilityId, LocationType.Room, new LocationCode("ROOM-B"), "Room B", now, now);

        context.Locations.AddRange(room, medical, otherRoom);
        await context.SaveChangesAsync();

        context.Locations.Add(new Location(
            primaryFacility.FacilityId,
            LocationType.Kennel,
            new LocationCode("KEN-A"),
            "Kennel A",
            now,
            now,
            parentLocationId: room.LocationId,
            gridRow: 0,
            gridColumn: 0));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var rooms = await service.ListRoomsAsync(primaryFacility.FacilityId);

        Assert.Equal(2, rooms.Count);
        Assert.Collection(
            rooms,
            item => Assert.Equal("MED-A", item.RoomCode.Value),
            item => Assert.Equal("ROOM-A", item.RoomCode.Value));
        Assert.DoesNotContain(rooms, x => x.RoomCode.Value == "KEN-A");
        Assert.DoesNotContain(rooms, x => x.FacilityId == otherFacility.FacilityId);
    }

    [Fact]
    public async Task Room_Map_Returns_Placed_And_Unplaced_Locations_Separately()
    {
        await using var context = CreateContext();
        var (facility, room) = await SeedFacilityWithRoomAsync(context);
        var now = Utc(2026, 4, 2, 8);

        context.Locations.AddRange(
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-PLACED"), "Placed Kennel", now, now, room.LocationId, gridRow: 0, gridColumn: 0),
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-UNPLACED"), "Unplaced Kennel", now, now, room.LocationId),
            new Location(facility.FacilityId, LocationType.Other, new LocationCode("OTHER-UNPLACED"), "Overflow Crate", now, now, room.LocationId));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetRoomMapAsync(facility.FacilityId, room.LocationId);

        Assert.NotNull(result);
        Assert.Single(result!.PlacedLocations);
        Assert.Equal("KEN-PLACED", result.PlacedLocations[0].LocationCode.Value);
        Assert.Equal(2, result.UnplacedLocations.Count);
        Assert.Contains(result.UnplacedLocations, x => x.LocationCode.Value == "KEN-UNPLACED");
        Assert.Contains(result.UnplacedLocations, x => x.LocationCode.Value == "OTHER-UNPLACED");
    }

    [Fact]
    public async Task Placed_Locations_Are_Returned_In_Stable_Grid_Order()
    {
        await using var context = CreateContext();
        var (facility, room) = await SeedFacilityWithRoomAsync(context);
        var now = Utc(2026, 4, 2, 8);

        context.Locations.AddRange(
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-C"), "C", now, now, room.LocationId, gridRow: 1, gridColumn: 0, stackLevel: 0),
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-B"), "B", now, now, room.LocationId, gridRow: 0, gridColumn: 1, stackLevel: 1),
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-A"), "A", now, now, room.LocationId, gridRow: 0, gridColumn: 1, stackLevel: 0),
            new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-D"), "D", now, now, room.LocationId, gridRow: 0, gridColumn: 2, stackLevel: 0));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetRoomMapAsync(facility.FacilityId, room.LocationId);

        Assert.NotNull(result);
        Assert.Equal(
            ["KEN-A", "KEN-B", "KEN-D", "KEN-C"],
            result!.PlacedLocations.Select(x => x.LocationCode.Value).ToArray());
    }

    [Fact]
    public async Task Current_Occupancy_Count_Is_Based_On_Open_Current_Movement_State()
    {
        await using var context = CreateContext();
        var (facility, room) = await SeedFacilityWithRoomAsync(context);
        var now = Utc(2026, 4, 2, 8);

        var kennelOne = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var kennelTwo = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-2"), "Kennel 2", now, now, room.LocationId, gridRow: 0, gridColumn: 1);
        context.Locations.AddRange(kennelOne, kennelTwo);

        var roomAnimal = new Animal(new AnimalCode("A-ROOM"), now, now, name: "Room Dog");
        var kennelAnimalOne = new Animal(new AnimalCode("A-1"), now, now, name: "One");
        var kennelAnimalTwo = new Animal(new AnimalCode("A-2"), now, now, name: "Two");
        var movedAnimal = new Animal(new AnimalCode("A-3"), now, now, name: "Moved");
        context.Animals.AddRange(roomAnimal, kennelAnimalOne, kennelAnimalTwo, movedAnimal);
        await context.SaveChangesAsync();

        context.MovementEvents.AddRange(
            new MovementEvent(roomAnimal.AnimalId, room.LocationId, now.AddHours(-1), now, now),
            new MovementEvent(kennelAnimalOne.AnimalId, kennelOne.LocationId, now.AddHours(-2), now, now),
            new MovementEvent(kennelAnimalTwo.AnimalId, kennelOne.LocationId, now.AddHours(-3), now, now),
            new MovementEvent(movedAnimal.AnimalId, kennelTwo.LocationId, now.AddHours(-4), now, now, endUtc: now.AddHours(-1)));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetRoomMapAsync(facility.FacilityId, room.LocationId);

        Assert.NotNull(result);
        Assert.Equal(1, result!.Room.CurrentOccupancyCount);
        Assert.Equal(2, result.PlacedLocations.Single(x => x.LocationCode.Value == "KEN-1").CurrentOccupancyCount);
        Assert.Equal(0, result.PlacedLocations.Single(x => x.LocationCode.Value == "KEN-2").CurrentOccupancyCount);
    }

    [Fact]
    public async Task Explicit_Links_Are_Exposed_Only_From_Stored_Data_Not_Inferred_From_Coordinates()
    {
        await using var context = CreateContext();
        var (facility, room) = await SeedFacilityWithRoomAsync(context);
        var now = Utc(2026, 4, 2, 8);

        var hallway = new Location(facility.FacilityId, LocationType.Hallway, new LocationCode("HALL-1"), "Hallway 1", now, now);
        var kennelOne = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var kennelTwo = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-2"), "Kennel 2", now, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var kennelThree = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-3"), "Kennel 3", now, now, room.LocationId, gridRow: 0, gridColumn: 2);
        context.Locations.AddRange(hallway, kennelOne, kennelTwo, kennelThree);
        await context.SaveChangesAsync();

        context.LocationLinks.AddRange(
            new LocationLink(facility.FacilityId, room.LocationId, hallway.LocationId, LinkType.Connected, now, now, sourceReference: "layout"),
            new LocationLink(facility.FacilityId, kennelTwo.LocationId, kennelThree.LocationId, LinkType.AdjacentRight, now, now, sourceReference: "layout"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.GetRoomMapAsync(facility.FacilityId, room.LocationId);

        Assert.NotNull(result);
        Assert.Contains(result!.Room.Links, x => x.ToLocationCode.Value == "HALL-1" && x.LinkType == LinkType.Connected);
        Assert.Empty(result.PlacedLocations.Single(x => x.LocationCode.Value == "KEN-1").Links);
        Assert.Contains(
            result.PlacedLocations.Single(x => x.LocationCode.Value == "KEN-2").Links,
            x => x.ToLocationCode.Value == "KEN-3" && x.LinkType == LinkType.AdjacentRight);
        Assert.DoesNotContain(
            result.PlacedLocations.Single(x => x.LocationCode.Value == "KEN-2").Links,
            x => x.ToLocationCode.Value == "KEN-1");
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static FacilityMapReadService CreateService(KennelTraceDbContext context) => new(context);

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static async Task<(Facility Facility, Location Room)> SeedFacilityWithRoomAsync(KennelTraceDbContext context)
    {
        var now = Utc(2026, 4, 2, 8);
        var facility = new Facility(new FacilityCode($"FAC-{Guid.NewGuid():N}".Substring(0, 12)), "Main Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode($"ROOM-{Guid.NewGuid():N}".Substring(0, 12)), "Adoption Room", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        return (facility, room);
    }
}
