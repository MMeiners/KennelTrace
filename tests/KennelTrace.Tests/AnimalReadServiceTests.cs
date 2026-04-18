using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class AnimalReadServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_AnimalRead_{Guid.NewGuid():N}";
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
    public async Task Lookup_Supports_AnimalNumber_Search()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);

        context.Animals.AddRange(
            new Animal(new AnimalCode("A-100"), now, now, name: "Biscuit"),
            new Animal(new AnimalCode("A-200"), now, now, name: "Scout"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var results = await service.LookupAnimalsAsync("A-100");

        Assert.Single(results);
        Assert.Equal("A-100", results[0].AnimalNumber.Value);
        Assert.Equal("Biscuit", results[0].Name);
    }

    [Fact]
    public async Task Lookup_Supports_Name_Search()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);

        context.Animals.AddRange(
            new Animal(new AnimalCode("A-100"), now, now, name: "Biscuit"),
            new Animal(new AnimalCode("A-200"), now, now, name: "Scout"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var results = await service.LookupAnimalsAsync("Scout");

        Assert.Single(results);
        Assert.Equal("A-200", results[0].AnimalNumber.Value);
        Assert.Equal("Scout", results[0].Name);
    }

    [Fact]
    public async Task Lookup_Supports_Formatted_Display_Label_Search()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);

        context.Animals.AddRange(
            new Animal(new AnimalCode("A-100"), now, now, name: "Biscuit"),
            new Animal(new AnimalCode("A-200"), now, now, name: "Scout"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var results = await service.LookupAnimalsAsync("A-100 - Biscuit");

        Assert.Single(results);
        Assert.Equal("A-100", results[0].AnimalNumber.Value);
        Assert.Equal("Biscuit", results[0].Name);
    }

    [Fact]
    public async Task Move_Location_Options_Include_All_Facilities_And_Keep_Inactive_Locations_Visible()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);
        var phoenix = await AddFacilityAsync(context, "PHX", "Phoenix Shelter", now);
        var tucson = await AddFacilityAsync(context, "TUC", "Tucson Shelter", now);
        var phoenixRoom = await AddLocationAsync(context, phoenix.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var activeKennel = await AddLocationAsync(context, phoenix.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, phoenixRoom.LocationId, gridRow: 0, gridColumn: 0);
        var inactiveIsolation = await AddLocationAsync(context, tucson.FacilityId, "ISO-1", "Isolation 1", LocationType.Isolation, now);
        inactiveIsolation.Deactivate(now.AddHours(1));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var locations = await service.ListMoveLocationsAsync();

        Assert.Equal([activeKennel.LocationId, phoenixRoom.LocationId, inactiveIsolation.LocationId], locations.Select(x => x.LocationId).ToArray());
        Assert.Equal("PHX", locations[0].FacilityCode.Value);
        Assert.True(locations[0].IsActive);
        Assert.Equal(LocationType.Kennel, locations[0].LocationType);
        Assert.Equal("TUC", locations[^1].FacilityCode.Value);
        Assert.False(locations[^1].IsActive);
    }

    [Fact]
    public async Task Detail_Returns_Animal_Data_When_There_Is_No_Current_Placement()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Shelter", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var animal = new Animal(
            new AnimalCode("A-100"),
            now,
            now,
            name: "Biscuit",
            species: "Dog",
            sex: "Female",
            breed: "Mix",
            dateOfBirth: new DateOnly(2024, 3, 2),
            notes: "Friendly");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(
            animal.AnimalId,
            kennel.LocationId,
            now.AddHours(-4),
            now,
            now,
            endUtc: now.AddHours(-1),
            movementReason: "Transfer"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var detail = await service.GetAnimalDetailAsync(animal.AnimalId);

        Assert.NotNull(detail);
        Assert.Equal("A-100", detail!.AnimalNumber.Value);
        Assert.Equal("Biscuit", detail.Name);
        Assert.Equal("Dog", detail.Species);
        Assert.Equal("Female", detail.Sex);
        Assert.Equal("Mix", detail.Breed);
        Assert.Equal(new DateOnly(2024, 3, 2), detail.DateOfBirth);
        Assert.Equal("Friendly", detail.Notes);
        Assert.Null(detail.CurrentPlacement);
        Assert.Single(detail.MovementHistory);
        Assert.Equal("PHX", detail.MovementHistory[0].FacilityCode.Value);
        Assert.Equal("KEN-1", detail.MovementHistory[0].LocationCode.Value);
    }

    [Fact]
    public async Task Current_Placement_Is_Derived_From_The_Open_Stay()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Shelter", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ISO-A", "Isolation A", LocationType.Isolation, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var animal = new Animal(new AnimalCode("A-200"), now, now, name: "Scout");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        context.MovementEvents.AddRange(
            new MovementEvent(animal.AnimalId, kennelOne.LocationId, now.AddHours(-6), now, now, endUtc: now.AddHours(-2), movementReason: "Intake"),
            new MovementEvent(animal.AnimalId, kennelTwo.LocationId, now.AddHours(-2), now, now, movementReason: "Isolation"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var detail = await service.GetAnimalDetailAsync(animal.AnimalId);

        Assert.NotNull(detail);
        Assert.NotNull(detail!.CurrentPlacement);
        Assert.Equal("PHX", detail.CurrentPlacement!.FacilityCode.Value);
        Assert.Equal("Phoenix Shelter", detail.CurrentPlacement.FacilityName);
        Assert.Equal("KEN-2", detail.CurrentPlacement.LocationCode.Value);
        Assert.Equal("Kennel 2", detail.CurrentPlacement.LocationName);
        Assert.Equal(LocationType.Kennel, detail.CurrentPlacement.LocationType);
        Assert.Equal(room.LocationId, detail.CurrentPlacement.RoomLocationId);
        Assert.Equal("ISO-A", detail.CurrentPlacement.RoomLocationCode!.Value.Value);
        Assert.Equal("Isolation A", detail.CurrentPlacement.RoomName);
        Assert.Equal(LocationType.Isolation, detail.CurrentPlacement.RoomLocationType);
        Assert.Null(detail.MovementHistory[0].EndUtc);
    }

    [Fact]
    public async Task History_Is_Returned_In_Reverse_Chronological_Order()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Shelter", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var kennelThree = await AddLocationAsync(context, facility.FacilityId, "KEN-3", "Kennel 3", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 2);
        var animal = new Animal(new AnimalCode("A-300"), now, now, name: "Riley");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        context.MovementEvents.AddRange(
            new MovementEvent(animal.AnimalId, kennelOne.LocationId, now.AddDays(-2), now, now, endUtc: now.AddDays(-1)),
            new MovementEvent(animal.AnimalId, kennelTwo.LocationId, now.AddDays(-1), now, now, endUtc: now.AddHours(-4)),
            new MovementEvent(animal.AnimalId, kennelThree.LocationId, now.AddHours(-4), now, now));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var detail = await service.GetAnimalDetailAsync(animal.AnimalId);

        Assert.NotNull(detail);
        Assert.Equal(
            ["KEN-3", "KEN-2", "KEN-1"],
            detail!.MovementHistory.Select(x => x.LocationCode.Value).ToArray());
    }

    [Fact]
    public async Task History_Remains_Visible_When_A_Location_Becomes_Inactive()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 9);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Shelter", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-OLD", "Retired Kennel", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var animal = new Animal(new AnimalCode("A-400"), now, now, name: "Milo");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(
            animal.AnimalId,
            kennel.LocationId,
            now.AddDays(-3),
            now,
            now,
            endUtc: now.AddDays(-2),
            movementReason: "Historical"));
        await context.SaveChangesAsync();

        kennel.Deactivate(now.AddDays(-1));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var detail = await service.GetAnimalDetailAsync(animal.AnimalId);

        Assert.NotNull(detail);
        Assert.Single(detail!.MovementHistory);
        Assert.Equal("KEN-OLD", detail.MovementHistory[0].LocationCode.Value);
        Assert.Equal("Retired Kennel", detail.MovementHistory[0].LocationName);
        Assert.False(detail.MovementHistory[0].LocationIsActive);
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static AnimalReadService CreateService(KennelTraceDbContext context) => new(context);

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);

    private static async Task<Facility> AddFacilityAsync(KennelTraceDbContext context, string facilityCode, string name, DateTime now)
    {
        var facility = new Facility(new FacilityCode(facilityCode), name, "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();
        return facility;
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
}
