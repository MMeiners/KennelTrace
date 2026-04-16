using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Locations.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class LocationAdminServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_LocationAdmin_{Guid.NewGuid():N}";
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
    public async Task Admin_Can_Create_And_Edit_A_Location()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now, displayOrder: 1);
        var service = CreateService(context);
        var adminUser = CreateUser(KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var createResult = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-1",
                Name: "Kennel 1",
                ParentLocationId: room.LocationId,
                GridRow: 0,
                GridColumn: 1,
                StackLevel: 0,
                DisplayOrder: 2,
                IsActive: true,
                Notes: "New kennel"),
            adminUser);

        Assert.Equal(LocationSaveStatus.Success, createResult.Status);
        Assert.NotNull(createResult.Location);

        var updateResult = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: createResult.Location!.LocationId,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-1A",
                Name: "Kennel 1A",
                ParentLocationId: room.LocationId,
                GridRow: null,
                GridColumn: null,
                StackLevel: 2,
                DisplayOrder: 3,
                IsActive: false,
                Notes: "Deactivated kennel"),
            adminUser);

        Assert.Equal(LocationSaveStatus.Success, updateResult.Status);

        await using var verificationContext = CreateContext();
        var location = await verificationContext.Locations.SingleAsync(x => x.LocationCode == new LocationCode("KEN-1A"));

        Assert.Equal("Kennel 1A", location.Name);
        Assert.Null(location.GridRow);
        Assert.Null(location.GridColumn);
        Assert.Equal(2, location.StackLevel);
        Assert.Equal(3, location.DisplayOrder);
        Assert.False(location.IsActive);
        Assert.Equal("Deactivated kennel", location.Notes);
    }

    [Fact]
    public async Task Parent_Must_Belong_To_The_Same_Facility()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var firstFacility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var secondFacility = await AddFacilityAsync(context, "TUC", "Tucson", now);
        var secondFacilityRoom = await AddLocationAsync(context, secondFacility.FacilityId, "ROOM-B", "Room B", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: firstFacility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-1",
                Name: "Kennel 1",
                ParentLocationId: secondFacilityRoom.LocationId,
                GridRow: 0,
                GridColumn: 0,
                StackLevel: 0,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("Parent and child must belong to the same facility.", result.ValidationErrors[nameof(LocationSaveRequest.ParentLocationId)]);
    }

    [Fact]
    public async Task Location_Cannot_Parent_Itself()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                room.LocationId,
                facility.FacilityId,
                LocationType.Room,
                "ROOM-A",
                "Room A",
                room.LocationId,
                null,
                null,
                0,
                null,
                true,
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("A location cannot parent itself.", result.ValidationErrors[nameof(LocationSaveRequest.ParentLocationId)]);
    }

    [Fact]
    public async Task Parent_Chains_Cannot_Be_Cyclic()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var rootRoom = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var childRoom = await AddLocationAsync(context, facility.FacilityId, "ROOM-B", "Room B", LocationType.Room, now, parentLocationId: rootRoom.LocationId);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                rootRoom.LocationId,
                facility.FacilityId,
                LocationType.Room,
                "ROOM-A",
                "Room A",
                childRoom.LocationId,
                null,
                null,
                0,
                null,
                true,
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("Parent chains must not be cyclic.", result.ValidationErrors[nameof(LocationSaveRequest.ParentLocationId)]);
    }

    [Fact]
    public async Task Allowed_Parent_Child_Combinations_Are_Enforced()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var hallway = await AddLocationAsync(context, facility.FacilityId, "HALL-1", "Hallway 1", LocationType.Hallway, now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, parentLocationId: room.LocationId);
        var service = CreateService(context);

        var invalidChildResult = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-2",
                Name: "Kennel 2",
                ParentLocationId: hallway.LocationId,
                GridRow: 0,
                GridColumn: 0,
                StackLevel: 0,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, invalidChildResult.Status);
        Assert.Contains("Kennels must have a valid room-like parent.", invalidChildResult.ValidationErrors[nameof(LocationSaveRequest.ParentLocationId)]);

        var invalidParentTypeChange = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: room.LocationId,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Hallway,
                LocationCode: "ROOM-A",
                Name: "Room A",
                ParentLocationId: null,
                GridRow: null,
                GridColumn: null,
                StackLevel: 0,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, invalidParentTypeChange.Status);
        Assert.Contains(
            "Locations with child locations must remain a valid room-like parent type.",
            invalidParentTypeChange.ValidationErrors[nameof(LocationSaveRequest.LocationType)]);
    }

    [Fact]
    public async Task Location_Code_Must_Be_Unique_Within_A_Facility()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Room,
                LocationCode: "ROOM-A",
                Name: "Duplicate Room",
                ParentLocationId: null,
                GridRow: null,
                GridColumn: null,
                StackLevel: 0,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains(
            "Location code must be unique within the facility.",
            result.ValidationErrors[nameof(LocationSaveRequest.LocationCode)]);
    }

    [Fact]
    public async Task Kennel_Placement_Requires_Paired_Row_And_Column()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-PAIR",
                Name: "Kennel Pair",
                ParentLocationId: room.LocationId,
                GridRow: 1,
                GridColumn: null,
                StackLevel: 0,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("GridRow and GridColumn must both be populated or both be null.", result.ValidationErrors[nameof(LocationSaveRequest.GridRow)]);
    }

    [Fact]
    public async Task Kennel_Placement_Rejects_Negative_Grid_Values()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-NEG",
                Name: "Negative Kennel",
                ParentLocationId: room.LocationId,
                GridRow: -1,
                GridColumn: 0,
                StackLevel: -1,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("Grid row cannot be negative.", result.ValidationErrors[nameof(LocationSaveRequest.GridRow)]);
        Assert.Contains("Stack level cannot be negative.", result.ValidationErrors[nameof(LocationSaveRequest.StackLevel)]);
    }

    [Fact]
    public async Task Active_Kennels_Cannot_Collide_In_The_Same_Room_Position()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId, null, 0, 0, 1);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationSaveRequest(
                LocationId: null,
                FacilityId: facility.FacilityId,
                LocationType: LocationType.Kennel,
                LocationCode: "KEN-2",
                Name: "Kennel 2",
                ParentLocationId: room.LocationId,
                GridRow: 0,
                GridColumn: 0,
                StackLevel: 1,
                DisplayOrder: null,
                IsActive: true,
                Notes: null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationSaveStatus.ValidationFailed, result.Status);
        Assert.Contains(
            "Another active kennel already uses that room, row, column, and stack position.",
            result.ValidationErrors[nameof(LocationSaveRequest.GridRow)]);
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static LocationAdminService CreateService(KennelTraceDbContext context) =>
        new(context, CreateAuthorizationService());

    private static IAuthorizationService CreateAuthorizationService()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationBuilder()
            .AddPolicy(KennelTracePolicies.AdminOnly, policy => policy.RequireRole(KennelTraceRoles.Admin));

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal CreateUser(params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };
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
        int? displayOrder = null,
        int? gridRow = null,
        int? gridColumn = null,
        int stackLevel = 0)
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
            gridColumn: gridColumn,
            stackLevel: stackLevel,
            displayOrder: displayOrder);

        context.Locations.Add(location);
        await context.SaveChangesAsync();
        return location;
    }
}
