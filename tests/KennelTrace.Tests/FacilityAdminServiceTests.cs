using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Facilities.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class FacilityAdminServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_FacilityAdmin_{Guid.NewGuid():N}";
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
    public async Task Admin_Can_Create_And_Update_A_Facility()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var adminUser = CreateUser(KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var createResult = await service.SaveAsync(
            new FacilitySaveRequest(
                FacilityId: null,
                FacilityCode: "PHX-MAIN",
                Name: "Phoenix Main Shelter",
                TimeZoneId: "America/Phoenix",
                IsActive: true,
                Notes: "Initial record"),
            adminUser);

        Assert.Equal(FacilitySaveStatus.Success, createResult.Status);
        Assert.NotNull(createResult.Facility);

        var createdFacilityId = createResult.Facility!.FacilityId;

        await Task.Delay(20);

        var updateResult = await service.SaveAsync(
            new FacilitySaveRequest(
                FacilityId: createdFacilityId,
                FacilityCode: "PHX-WEST",
                Name: "Phoenix West Shelter",
                TimeZoneId: "America/Phoenix",
                IsActive: false,
                Notes: "Renamed and deactivated"),
            adminUser);

        Assert.Equal(FacilitySaveStatus.Success, updateResult.Status);

        await using var verificationContext = CreateContext();
        var facility = await verificationContext.Facilities.SingleAsync();

        Assert.Equal("PHX-WEST", facility.FacilityCode.Value);
        Assert.Equal("Phoenix West Shelter", facility.Name);
        Assert.Equal("America/Phoenix", facility.TimeZoneId);
        Assert.False(facility.IsActive);
        Assert.Equal("Renamed and deactivated", facility.Notes);
        Assert.Equal(createResult.Facility.CreatedUtc, facility.CreatedUtc);
        Assert.True(facility.ModifiedUtc > createResult.Facility.ModifiedUtc);
    }

    [Fact]
    public async Task Duplicate_Facility_Code_Returns_Friendly_Validation_Message()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 15, 16);
        context.Facilities.Add(new Facility(new FacilityCode("PHX-MAIN"), "Existing", "America/Phoenix", now, now));
        await context.SaveChangesAsync();

        var service = CreateService(context);
        var adminUser = CreateUser(KennelTraceRoles.Admin);

        var result = await service.SaveAsync(
            new FacilitySaveRequest(
                FacilityId: null,
                FacilityCode: "PHX-MAIN",
                Name: "Duplicate",
                TimeZoneId: "America/Phoenix",
                IsActive: true,
                Notes: null),
            adminUser);

        Assert.Equal(FacilitySaveStatus.ValidationFailed, result.Status);
        Assert.True(result.ValidationErrors.TryGetValue(nameof(FacilitySaveRequest.FacilityCode), out var messages));
        Assert.Contains("Facility code must be unique.", messages);

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.Facilities.CountAsync());
    }

    [Fact]
    public async Task Non_Admin_Save_Is_Rejected_Server_Side()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        var readOnlyUser = CreateUser(KennelTraceRoles.ReadOnly);

        var result = await service.SaveAsync(
            new FacilitySaveRequest(
                FacilityId: null,
                FacilityCode: "TUC-MAIN",
                Name: "Tucson Main",
                TimeZoneId: "America/Phoenix",
                IsActive: true,
                Notes: null),
            readOnlyUser);

        Assert.Equal(FacilitySaveStatus.Forbidden, result.Status);

        await using var verificationContext = CreateContext();
        Assert.Equal(0, await verificationContext.Facilities.CountAsync());
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static FacilityAdminService CreateService(KennelTraceDbContext context) =>
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
}
