using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class SqlServerPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_Test_{Guid.NewGuid():N}";
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
    public async Task Can_Create_Database_From_Migrations_And_Persist_Valid_Rows()
    {
        await using var context = CreateContext();

        var migrations = await context.Database.GetAppliedMigrationsAsync();
        Assert.NotEmpty(migrations);

        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);
        var facility = new Facility(new FacilityCode("FAC-1"), "Main Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-1"), "Room 1", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        var kennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var animal = new Animal(new AnimalCode("A-100"), now, now, name: "Scout");
        context.AddRange(kennel, animal);
        await context.SaveChangesAsync();

        var movement = new MovementEvent(animal.AnimalId, kennel.LocationId, now, now, now, endUtc: now.AddHours(2));
        context.MovementEvents.Add(movement);
        await context.SaveChangesAsync();

        var stored = await context.MovementEvents.SingleAsync();
        Assert.Equal(animal.AnimalId, stored.AnimalId);
        Assert.Equal(kennel.LocationId, stored.LocationId);
    }

    [Fact]
    public async Task Active_Kennel_Grid_Positions_Must_Be_Unique_Within_A_Room()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-GRID"), "Grid Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-GRID"), "Room Grid", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        context.Locations.Add(new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-A"), "Kennel A", now, now, room.LocationId, gridRow: 1, gridColumn: 1));
        await context.SaveChangesAsync();

        context.Locations.Add(new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-B"), "Kennel B", now, now, room.LocationId, gridRow: 1, gridColumn: 1));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task Active_Directed_Location_Links_Must_Be_Unique_But_Inactive_History_Allows_Replacement()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-LINK"), "Link Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var leftRoom = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-L"), "Left", now, now);
        var rightRoom = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-R"), "Right", now, now);
        context.Locations.AddRange(leftRoom, rightRoom);
        await context.SaveChangesAsync();

        var activeLink = new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now, now);
        context.LocationLinks.Add(activeLink);
        await context.SaveChangesAsync();

        context.LocationLinks.Add(new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now, now));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        context.ChangeTracker.Clear();

        var existingLink = await context.LocationLinks.SingleAsync();
        existingLink.Deactivate(now.AddMinutes(5));
        await context.SaveChangesAsync();

        context.LocationLinks.Add(new LocationLink(facility.FacilityId, leftRoom.LocationId, rightRoom.LocationId, LinkType.Connected, now.AddMinutes(10), now.AddMinutes(10)));
        await context.SaveChangesAsync();

        Assert.Equal(2, await context.LocationLinks.CountAsync());
        Assert.Equal(1, await context.LocationLinks.CountAsync(x => x.IsActive));
    }

    [Fact]
    public async Task One_Open_Stay_Per_Animal_Is_Enforced_And_Half_Open_Queries_Do_Not_Treat_Handoff_As_Overlap()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 4, 1, 8, 0, 0, DateTimeKind.Utc);

        var facility = new Facility(new FacilityCode("FAC-MOVE"), "Movement Shelter", "America/Phoenix", now, now);
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();

        var room = new Location(facility.FacilityId, LocationType.Room, new LocationCode("ROOM-MOVE"), "Room Move", now, now);
        context.Locations.Add(room);
        await context.SaveChangesAsync();

        var firstKennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var secondKennel = new Location(facility.FacilityId, LocationType.Kennel, new LocationCode("KEN-2"), "Kennel 2", now, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var animal = new Animal(new AnimalCode("A-MOVE"), now, now, name: "Mover");
        context.AddRange(firstKennel, secondKennel, animal);
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, firstKennel.LocationId, now, now, now));
        await context.SaveChangesAsync();

        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, secondKennel.LocationId, now.AddHours(1), now.AddHours(1), now.AddHours(1)));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var openStay = await context.MovementEvents.SingleAsync();
        openStay.Close(now.AddHours(4), now.AddHours(4));
        await context.SaveChangesAsync();

        var secondStay = new MovementEvent(animal.AnimalId, secondKennel.LocationId, now.AddHours(4), now.AddHours(4), now.AddHours(4), endUtc: now.AddHours(8));
        context.MovementEvents.Add(secondStay);
        await context.SaveChangesAsync();

        var probeStart = now.AddHours(4);
        var probeEnd = now.AddHours(8);
        var overlappingIds = await context.MovementEvents
            .Where(x => x.StartUtc < probeEnd && probeStart < (x.EndUtc ?? DateTime.MaxValue))
            .Select(x => x.MovementEventId)
            .ToListAsync();

        Assert.DoesNotContain(openStay.MovementEventId, overlappingIds);
        Assert.Contains(secondStay.MovementEventId, overlappingIds);
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }
}
