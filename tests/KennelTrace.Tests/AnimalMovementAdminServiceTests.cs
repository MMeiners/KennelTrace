using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Animals.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class AnimalMovementAdminServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_AnimalMovement_{Guid.NewGuid():N}";
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
    public async Task First_Open_Stay_For_An_Animal_Is_Recorded()
    {
        await using var context = CreateContext();
        var placedAt = Utc(2026, 4, 17, 8, 15);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", placedAt);
        var room = await AddLocationAsync(context, facility.FacilityId, "INTAKE-A", "Intake A", LocationType.Intake, placedAt);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-101", "Kennel 101", LocationType.Kennel, placedAt, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-100", "Biscuit", placedAt);
        var service = CreateService(context);

        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennel.LocationId,
                placedAt,
                EndUtc: null,
                MovementReason: "Intake",
                Notes: "Initial placement"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);
        Assert.NotNull(result.Stay);

        await using var verificationContext = CreateContext();
        var stay = await verificationContext.MovementEvents.SingleAsync();

        Assert.Equal(animal.AnimalId, stay.AnimalId);
        Assert.Equal(kennel.LocationId, stay.LocationId);
        Assert.Equal(placedAt, stay.StartUtc);
        Assert.Null(stay.EndUtc);
        Assert.Equal("Intake", stay.MovementReason);
        Assert.Equal("Initial placement", stay.Notes);
    }

    [Fact]
    public async Task Later_Move_Closes_Prior_Open_Stay_And_Opens_The_New_Stay()
    {
        await using var context = CreateContext();
        var intakeAt = Utc(2026, 4, 17, 8, 0);
        var movedAt = Utc(2026, 4, 17, 12, 30);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", intakeAt);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, intakeAt);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-101", "Kennel 101", LocationType.Kennel, intakeAt, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-102", "Kennel 102", LocationType.Kennel, intakeAt, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-200", "Scout", intakeAt);
        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, kennelOne.LocationId, intakeAt, intakeAt, intakeAt, movementReason: "Intake"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennelTwo.LocationId,
                movedAt,
                EndUtc: null,
                MovementReason: "Medical hold",
                Notes: "Moved after exam"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var stays = await verificationContext.MovementEvents
            .Where(x => x.AnimalId == animal.AnimalId)
            .OrderBy(x => x.StartUtc)
            .ToListAsync();

        Assert.Equal(2, stays.Count);
        Assert.Equal(kennelOne.LocationId, stays[0].LocationId);
        Assert.Equal(intakeAt, stays[0].StartUtc);
        Assert.Equal(movedAt, stays[0].EndUtc);
        Assert.Equal(kennelTwo.LocationId, stays[1].LocationId);
        Assert.Equal(movedAt, stays[1].StartUtc);
        Assert.Null(stays[1].EndUtc);
        Assert.Equal("Medical hold", stays[1].MovementReason);
    }

    [Fact]
    public async Task Same_Timestamp_Handoff_Is_Allowed_For_Consecutive_Closed_Stays()
    {
        await using var context = CreateContext();
        var firstStart = Utc(2026, 4, 17, 8, 0);
        var handoffAt = Utc(2026, 4, 17, 12, 0);
        var secondEnd = Utc(2026, 4, 17, 15, 45);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", firstStart);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, firstStart);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-201", "Kennel 201", LocationType.Kennel, firstStart, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-202", "Kennel 202", LocationType.Kennel, firstStart, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-300", "Pepper", firstStart);
        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, kennelOne.LocationId, firstStart, firstStart, firstStart, handoffAt, "Intake"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennelTwo.LocationId,
                handoffAt,
                secondEnd,
                "Isolation",
                "Same-minute handoff"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var stays = await verificationContext.MovementEvents
            .Where(x => x.AnimalId == animal.AnimalId)
            .OrderBy(x => x.StartUtc)
            .ToListAsync();

        Assert.Equal(2, stays.Count);
        Assert.Equal(handoffAt, stays[0].EndUtc);
        Assert.Equal(handoffAt, stays[1].StartUtc);
        Assert.Equal(secondEnd, stays[1].EndUtc);
    }

    [Fact]
    public async Task Overlapping_Historical_Stay_Is_Rejected()
    {
        await using var context = CreateContext();
        var firstStart = Utc(2026, 4, 17, 8, 0);
        var firstEnd = Utc(2026, 4, 17, 12, 0);
        var overlappingStart = Utc(2026, 4, 17, 11, 30);
        var overlappingEnd = Utc(2026, 4, 17, 13, 0);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", firstStart);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, firstStart);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-301", "Kennel 301", LocationType.Kennel, firstStart, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-302", "Kennel 302", LocationType.Kennel, firstStart, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-400", "Mocha", firstStart);
        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, kennelOne.LocationId, firstStart, firstStart, firstStart, firstEnd, "Intake"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennelTwo.LocationId,
                overlappingStart,
                overlappingEnd,
                "Transfer",
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.ValidationFailed, result.Status);
        Assert.Contains(
            "The requested stay overlaps existing movement history for this animal.",
            result.ValidationErrors[nameof(RecordAnimalStayRequest.StartUtc)]);

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.MovementEvents.CountAsync());
    }

    [Fact]
    public async Task Second_Open_Stay_Is_Prevented_When_Request_Does_Not_Represent_A_Move()
    {
        await using var context = CreateContext();
        var currentStayStart = Utc(2026, 4, 17, 10, 0);
        var attemptedOpenStart = Utc(2026, 4, 17, 9, 30);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", currentStayStart);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, currentStayStart);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-401", "Kennel 401", LocationType.Kennel, currentStayStart, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-402", "Kennel 402", LocationType.Kennel, currentStayStart, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-500", "Ranger", currentStayStart);
        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, kennelOne.LocationId, currentStayStart, currentStayStart, currentStayStart, movementReason: "Current placement"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennelTwo.LocationId,
                attemptedOpenStart,
                EndUtc: null,
                MovementReason: "Backdated move",
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.ValidationFailed, result.Status);
        Assert.Contains(
            "The requested stay overlaps existing movement history for this animal.",
            result.ValidationErrors[nameof(RecordAnimalStayRequest.StartUtc)]);

        await using var verificationContext = CreateContext();
        var stays = await verificationContext.MovementEvents
            .Where(x => x.AnimalId == animal.AnimalId)
            .ToListAsync();

        Assert.Single(stays);
        Assert.Null(stays[0].EndUtc);
    }

    [Fact]
    public async Task Closed_Stay_Can_Be_Recorded_When_No_Current_Open_Stay_Exists()
    {
        await using var context = CreateContext();
        var startUtc = Utc(2026, 4, 17, 7, 45);
        var endUtc = Utc(2026, 4, 17, 11, 10);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", startUtc);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, startUtc);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-501", "Kennel 501", LocationType.Kennel, startUtc, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-600", "Nala", startUtc);
        var service = CreateService(context);

        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennel.LocationId,
                startUtc,
                endUtc,
                "Temporary housing",
                "No current stay should remain open"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var stays = await verificationContext.MovementEvents.ToListAsync();

        Assert.Single(stays);
        Assert.Equal(endUtc, stays[0].EndUtc);
        Assert.Equal(0, stays.Count(x => x.EndUtc is null));
    }

    [Fact]
    public async Task Consecutive_Moves_Across_Facilities_Are_Allowed()
    {
        await using var context = CreateContext();
        var firstStart = Utc(2026, 4, 17, 8, 0);
        var secondStart = Utc(2026, 4, 18, 9, 15);
        var firstFacility = await AddFacilityAsync(context, "PHX", "Phoenix Main", firstStart);
        var secondFacility = await AddFacilityAsync(context, "TUC", "Tucson Intake", firstStart);
        var firstRoom = await AddLocationAsync(context, firstFacility.FacilityId, "ROOM-A", "Room A", LocationType.Room, firstStart);
        var secondRoom = await AddLocationAsync(context, secondFacility.FacilityId, "INTAKE-B", "Intake B", LocationType.Intake, firstStart);
        var firstKennel = await AddLocationAsync(context, firstFacility.FacilityId, "KEN-601", "Kennel 601", LocationType.Kennel, firstStart, firstRoom.LocationId);
        var secondKennel = await AddLocationAsync(context, secondFacility.FacilityId, "KEN-602", "Kennel 602", LocationType.Kennel, firstStart, secondRoom.LocationId);
        var animal = await AddAnimalAsync(context, "A-700", "Clover", firstStart);
        context.MovementEvents.Add(new MovementEvent(animal.AnimalId, firstKennel.LocationId, firstStart, firstStart, firstStart, movementReason: "Initial facility"));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                secondKennel.LocationId,
                secondStart,
                EndUtc: null,
                MovementReason: "Transfer to Tucson",
                Notes: "Cross-facility move"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var rows = await (
                from stay in verificationContext.MovementEvents
                join location in verificationContext.Locations on stay.LocationId equals location.LocationId
                where stay.AnimalId == animal.AnimalId
                orderby stay.StartUtc
                select new
                {
                    stay.StartUtc,
                    stay.EndUtc,
                    stay.LocationId,
                    location.FacilityId
                })
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal(firstFacility.FacilityId, rows[0].FacilityId);
        Assert.Equal(secondStart, rows[0].EndUtc);
        Assert.Equal(secondFacility.FacilityId, rows[1].FacilityId);
        Assert.Null(rows[1].EndUtc);
    }

    [Fact]
    public async Task RecordedByUserId_Is_Populated_From_NameIdentifier_When_Available()
    {
        await using var context = CreateContext();
        var placedAt = Utc(2026, 4, 17, 14, 20);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", placedAt);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, placedAt);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-701", "Kennel 701", LocationType.Kennel, placedAt, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-800", "Luna", placedAt);
        var service = CreateService(context);

        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennel.LocationId,
                placedAt,
                EndUtc: null,
                MovementReason: "Owner surrender",
                Notes: null),
            CreateUser("movement-admin", "movement-admin", KennelTraceRoles.Admin));

        Assert.Equal(RecordAnimalStayStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var stay = await verificationContext.MovementEvents.SingleAsync();

        Assert.Equal("movement-admin", stay.RecordedByUserId);
    }

    [Fact]
    public async Task Non_Admin_Record_Is_Rejected_Server_Side()
    {
        await using var context = CreateContext();
        var placedAt = Utc(2026, 4, 17, 9, 0);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix Main", placedAt);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, placedAt);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-801", "Kennel 801", LocationType.Kennel, placedAt, room.LocationId);
        var animal = await AddAnimalAsync(context, "A-900", "Indy", placedAt);
        var service = CreateService(context);

        var result = await service.RecordStayAsync(
            new RecordAnimalStayRequest(
                animal.AnimalId,
                kennel.LocationId,
                placedAt,
                EndUtc: null,
                MovementReason: "Attempted write",
                Notes: null),
            CreateUser(KennelTraceRoles.ReadOnly));

        Assert.Equal(RecordAnimalStayStatus.Forbidden, result.Status);

        await using var verificationContext = CreateContext();
        Assert.Equal(0, await verificationContext.MovementEvents.CountAsync());
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static AnimalMovementAdminService CreateService(KennelTraceDbContext context) =>
        new(context, CreateAuthorizationService());

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder()
            .AddPolicy(KennelTracePolicies.AdminOnly, policy => policy.RequireRole(KennelTraceRoles.Admin));

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUser(params string[] roles) =>
        CreateUser("test-user", null, roles);

    private static ClaimsPrincipal CreateUser(string userName, string? userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, userName) };

        if (!string.IsNullOrWhiteSpace(userId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static string GetServerConnectionString()
    {
        var configured = Environment.GetEnvironmentVariable("KENNELTRACE_TEST_SQLSERVER");
        return string.IsNullOrWhiteSpace(configured)
            ? "Server=localhost;Integrated Security=true;Encrypt=False;TrustServerCertificate=true;MultipleActiveResultSets=True"
            : configured;
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

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
        int? parentLocationId = null)
    {
        var location = new Location(
            facilityId,
            locationType,
            new LocationCode(locationCode),
            name,
            now,
            now,
            parentLocationId);

        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }

    private static async Task<Animal> AddAnimalAsync(KennelTraceDbContext context, string animalNumber, string name, DateTime now)
    {
        var animal = new Animal(new AnimalCode(animalNumber), now, now, name: name);
        context.Animals.Add(animal);
        await context.SaveChangesAsync();
        return animal;
    }
}
