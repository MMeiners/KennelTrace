using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Domain.Features.Tracing;
using KennelTrace.Infrastructure.Features.Tracing.TracePage;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Tests;

public sealed class TracePageReadServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_TracePageRead_{Guid.NewGuid():N}";
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
    public async Task Active_Disease_Profile_Options_Require_Active_Profile_And_Active_Disease()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 16);

        var activeDisease = new Disease(new DiseaseCode("CIV"), "Canine Influenza", now, now);
        var inactiveDisease = new Disease(new DiseaseCode("PARVO"), "Parvovirus", now, now, isActive: false);
        var activeDiseaseWithInactiveProfile = new Disease(new DiseaseCode("URI"), "Upper Respiratory", now, now);
        context.Diseases.AddRange(activeDisease, inactiveDisease, activeDiseaseWithInactiveProfile);
        await context.SaveChangesAsync();

        context.DiseaseTraceProfiles.AddRange(
            new DiseaseTraceProfile(activeDisease.DiseaseId, 72, now, now),
            new DiseaseTraceProfile(inactiveDisease.DiseaseId, 48, now, now),
            new DiseaseTraceProfile(activeDiseaseWithInactiveProfile.DiseaseId, 24, now, now, isActive: false));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var options = await service.ListActiveDiseaseProfilesAsync();

        Assert.Single(options);
        Assert.Equal("CIV", options[0].DiseaseCode.Value);
        Assert.Equal("Canine Influenza", options[0].DiseaseName);
        Assert.Equal(72, options[0].DefaultLookbackHours);
    }

    [Fact]
    public async Task Location_Scope_Options_Load_Active_Persisted_Locations_With_Facility_Context()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 16);

        var alphaFacility = new Facility(new FacilityCode("ALPHA"), "Alpha Shelter", "America/Phoenix", now, now);
        var betaFacility = new Facility(new FacilityCode("BETA"), "Beta Shelter", "America/Phoenix", now, now);
        context.Facilities.AddRange(alphaFacility, betaFacility);
        await context.SaveChangesAsync();

        var alphaRoom = new Location(alphaFacility.FacilityId, LocationType.Room, new LocationCode("ROOM-A"), "Room A", now, now);
        context.Locations.Add(alphaRoom);
        await context.SaveChangesAsync();

        var alphaKennel = new Location(alphaFacility.FacilityId, LocationType.Kennel, new LocationCode("KEN-1"), "Kennel 1", now, now, alphaRoom.LocationId, gridRow: 0, gridColumn: 0);
        var retiredLocation = new Location(betaFacility.FacilityId, LocationType.Isolation, new LocationCode("ISO-OLD"), "Old Isolation", now, now);
        var betaMedical = new Location(betaFacility.FacilityId, LocationType.Medical, new LocationCode("MED-1"), "Medical 1", now, now);
        context.Locations.AddRange(alphaKennel, retiredLocation, betaMedical);
        await context.SaveChangesAsync();

        retiredLocation.Deactivate(now.AddHours(1));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var options = await service.ListLocationScopeOptionsAsync();

        Assert.Equal(
            ["Alpha Shelter:Kennel 1", "Alpha Shelter:Room A", "Beta Shelter:Medical 1"],
            options.Select(x => $"{x.FacilityName}:{x.LocationName}").ToArray());
        Assert.Equal("ALPHA", options[0].FacilityCode.Value);
        Assert.Equal(LocationType.Kennel, options[0].LocationType);
        Assert.DoesNotContain(options, x => x.LocationCode.Value == "ISO-OLD");
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static TracePageReadService CreateService(KennelTraceDbContext context) => new(context);

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
