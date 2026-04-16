using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Locations.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class LocationLinkAdminServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_LocationLinks_{Guid.NewGuid():N}";
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
    public async Task Create_Adds_Directed_Row_And_Reciprocal_Row()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationLinkSaveRequest(
                facility.FacilityId,
                kennelOne.LocationId,
                kennelTwo.LocationId,
                LinkType.AdjacentRight,
                AllowTopologyEndpointOverride: false,
                SourceReference: "whiteboard",
                Notes: "Created manually"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationLinkSaveStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var links = await verificationContext.LocationLinks
            .Where(x => x.FacilityId == facility.FacilityId)
            .OrderBy(x => x.FromLocationId)
            .ThenBy(x => x.ToLocationId)
            .ToListAsync();

        Assert.Collection(
            links,
            link =>
            {
                Assert.Equal(kennelOne.LocationId, link.FromLocationId);
                Assert.Equal(kennelTwo.LocationId, link.ToLocationId);
                Assert.Equal(LinkType.AdjacentRight, link.LinkType);
                Assert.True(link.IsActive);
                Assert.Equal(SourceType.Manual, link.SourceType);
                Assert.Equal("whiteboard", link.SourceReference);
                Assert.Equal("Created manually", link.Notes);
            },
            link =>
            {
                Assert.Equal(kennelTwo.LocationId, link.FromLocationId);
                Assert.Equal(kennelOne.LocationId, link.ToLocationId);
                Assert.Equal(LinkType.AdjacentLeft, link.LinkType);
                Assert.True(link.IsActive);
                Assert.Equal(SourceType.Manual, link.SourceType);
                Assert.Equal("whiteboard", link.SourceReference);
                Assert.Equal("Created manually", link.Notes);
            });
    }

    [Fact]
    public async Task Remove_Deactivates_Directed_Row_And_Reciprocal_Row()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId);
        var service = CreateService(context);

        await service.SaveAsync(
            new LocationLinkSaveRequest(
                facility.FacilityId,
                kennelOne.LocationId,
                kennelTwo.LocationId,
                LinkType.AdjacentRight,
                false,
                null,
                null),
            CreateUser(KennelTraceRoles.Admin));

        var removeResult = await service.RemoveAsync(
            new LocationLinkRemoveRequest(
                facility.FacilityId,
                kennelOne.LocationId,
                kennelTwo.LocationId,
                LinkType.AdjacentRight),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationLinkRemoveStatus.Success, removeResult.Status);

        await using var verificationContext = CreateContext();
        var links = await verificationContext.LocationLinks
            .Where(x => x.FacilityId == facility.FacilityId)
            .OrderBy(x => x.LocationLinkId)
            .ToListAsync();

        Assert.Equal(2, links.Count);
        Assert.All(links, link => Assert.False(link.IsActive));
    }

    [Fact]
    public async Task Duplicate_Active_Directed_Link_Is_Rejected()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId);
        var service = CreateService(context);
        var request = new LocationLinkSaveRequest(
            facility.FacilityId,
            kennelOne.LocationId,
            kennelTwo.LocationId,
            LinkType.AdjacentRight,
            false,
            null,
            null);

        await service.SaveAsync(request, CreateUser(KennelTraceRoles.Admin));
        var duplicateResult = await service.SaveAsync(request, CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationLinkSaveStatus.ValidationFailed, duplicateResult.Status);
        Assert.Contains(
            "An active link with the same source, target, and type already exists.",
            duplicateResult.ValidationErrors[nameof(LocationLinkSaveRequest.LinkType)]);
    }

    [Fact]
    public async Task Endpoint_Family_Validation_Is_Enforced()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennel = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId);
        var hallway = await AddLocationAsync(context, facility.FacilityId, "HALL-1", "Hallway 1", LocationType.Hallway, now);
        var service = CreateService(context);

        var adjacencyResult = await service.SaveAsync(
            new LocationLinkSaveRequest(
                facility.FacilityId,
                room.LocationId,
                kennel.LocationId,
                LinkType.AdjacentOther,
                false,
                null,
                null),
            CreateUser(KennelTraceRoles.Admin));

        var topologyResult = await service.SaveAsync(
            new LocationLinkSaveRequest(
                facility.FacilityId,
                kennel.LocationId,
                hallway.LocationId,
                LinkType.TransportPath,
                false,
                null,
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationLinkSaveStatus.ValidationFailed, adjacencyResult.Status);
        Assert.Contains("Adjacency links must connect kennel locations.", adjacencyResult.ValidationErrors[nameof(LocationLinkSaveRequest.LinkType)]);

        Assert.Equal(LocationLinkSaveStatus.ValidationFailed, topologyResult.Status);
        Assert.Contains(
            "Topology links default to non-kennel spaces. Enable the explicit override to save an unusual topology link.",
            topologyResult.ValidationErrors[nameof(LocationLinkSaveRequest.AllowTopologyEndpointOverride)]);
    }

    [Fact]
    public async Task Cross_Facility_Links_Are_Rejected()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var firstFacility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var secondFacility = await AddFacilityAsync(context, "TUC", "Tucson", now);
        var firstRoom = await AddLocationAsync(context, firstFacility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var secondRoom = await AddLocationAsync(context, secondFacility.FacilityId, "ROOM-B", "Room B", LocationType.Room, now);
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new LocationLinkSaveRequest(
                firstFacility.FacilityId,
                firstRoom.LocationId,
                secondRoom.LocationId,
                LinkType.Connected,
                false,
                null,
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(LocationLinkSaveStatus.ValidationFailed, result.Status);
        Assert.Contains(
            "Target location must belong to the selected facility.",
            result.ValidationErrors[nameof(LocationLinkSaveRequest.ToLocationId)]);
    }

    [Fact]
    public async Task Admin_Added_Link_Is_Visible_In_Read_Only_Room_Map_Data()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 18);
        var facility = await AddFacilityAsync(context, "PHX", "Phoenix", now);
        var room = await AddLocationAsync(context, facility.FacilityId, "ROOM-A", "Room A", LocationType.Room, now);
        var kennelOne = await AddLocationAsync(context, facility.FacilityId, "KEN-1", "Kennel 1", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 0);
        var kennelTwo = await AddLocationAsync(context, facility.FacilityId, "KEN-2", "Kennel 2", LocationType.Kennel, now, room.LocationId, gridRow: 0, gridColumn: 1);
        var adminService = CreateService(context);
        var mapService = new FacilityMapReadService(context);

        await adminService.SaveAsync(
            new LocationLinkSaveRequest(
                facility.FacilityId,
                kennelOne.LocationId,
                kennelTwo.LocationId,
                LinkType.AdjacentRight,
                false,
                "layout review",
                "Confirmed by admin"),
            CreateUser(KennelTraceRoles.Admin));

        var roomMap = await mapService.GetRoomMapAsync(facility.FacilityId, room.LocationId);

        Assert.NotNull(roomMap);
        var firstKennelDetail = roomMap!.PlacedLocations.Single(x => x.LocationId == kennelOne.LocationId);
        var secondKennelDetail = roomMap.PlacedLocations.Single(x => x.LocationId == kennelTwo.LocationId);

        Assert.Contains(firstKennelDetail.Links, x => x.ToLocationId == kennelTwo.LocationId && x.LinkType == LinkType.AdjacentRight && x.SourceType == SourceType.Manual);
        Assert.Contains(secondKennelDetail.Links, x => x.ToLocationId == kennelOne.LocationId && x.LinkType == LinkType.AdjacentLeft && x.SourceType == SourceType.Manual);
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static LocationLinkAdminService CreateService(KennelTraceDbContext context) =>
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
