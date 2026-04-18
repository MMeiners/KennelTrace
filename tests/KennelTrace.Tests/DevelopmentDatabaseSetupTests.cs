using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Tracing;
using KennelTrace.Infrastructure.Features.Tracing.TracePage;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Development;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class DevelopmentDatabaseSetupTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_DevSetup_{Guid.NewGuid():N}";
    private string _connectionString = null!;

    public Task InitializeAsync()
    {
        var builder = new SqlConnectionStringBuilder(GetServerConnectionString())
        {
            InitialCatalog = _databaseName
        };

        _connectionString = builder.ConnectionString;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        SqlConnection.ClearAllPools();

        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Apply_Development_Database_Setup_Seeds_Trace_Profile_And_Remains_Idempotent()
    {
        using var services = CreateServices();

        await services.ApplyDevelopmentDatabaseSetupAsync();
        await services.ApplyDevelopmentDatabaseSetupAsync();

        await using var context = CreateContext();
        var disease = await context.Diseases
            .AsNoTracking()
            .SingleAsync(x => x.DiseaseCode == new DiseaseCode("PILOT_RESP"));
        var profile = await context.DiseaseTraceProfiles
            .AsNoTracking()
            .Include(x => x.TopologyLinkTypes)
            .SingleAsync(x => x.DiseaseId == disease.DiseaseId);

        Assert.Equal("Pilot Respiratory", disease.Name);
        Assert.True(disease.IsActive);
        Assert.Equal("Development/test seed profile for end-to-end contact trace verification. Not a clinically validated protocol.", disease.Notes);

        Assert.True(profile.IsActive);
        Assert.Equal(72, profile.DefaultLookbackHours);
        Assert.True(profile.IncludeSameLocation);
        Assert.True(profile.IncludeSameRoom);
        Assert.True(profile.IncludeAdjacent);
        Assert.Equal(1, profile.AdjacencyDepth);
        Assert.False(profile.IncludeTopologyLinks);
        Assert.Equal(0, profile.TopologyDepth);
        Assert.Equal("Development/test profile for MVP trace verification.", profile.Notes);
        Assert.Empty(profile.TopologyLinkTypes);

        Assert.Equal(1, await context.Diseases.CountAsync(x => x.DiseaseCode == new DiseaseCode("PILOT_RESP")));
        Assert.Equal(1, await context.DiseaseTraceProfiles.CountAsync(x => x.DiseaseId == disease.DiseaseId));
        Assert.Equal(0, await context.DiseaseTraceProfileTopologyLinkTypes.CountAsync(x => x.DiseaseTraceProfileId == profile.DiseaseTraceProfileId));

        var tracePageReadService = new TracePageReadService(context);
        var options = await tracePageReadService.ListActiveDiseaseProfilesAsync();

        Assert.Contains(options, x =>
            x.DiseaseCode == new DiseaseCode("PILOT_RESP")
            && x.DiseaseName == "Pilot Respiratory"
            && x.DefaultLookbackHours == 72);
    }

    [Fact]
    public async Task Apply_Development_Database_Setup_Seeds_Trace_Profile_Even_When_Dev_Facility_Already_Exists()
    {
        await using (var context = CreateContext())
        {
            await context.Database.MigrateAsync();

            var now = Utc(2026, 4, 18, 10);
            context.Facilities.Add(new Facility(
                new FacilityCode("DEV-PHX"),
                "Development Shelter",
                "America/Phoenix",
                now,
                now,
                notes: "Pre-existing development facility for bootstrap regression coverage."));
            await context.SaveChangesAsync();
        }

        using var services = CreateServices();
        await services.ApplyDevelopmentDatabaseSetupAsync();

        await using var verificationContext = CreateContext();
        var disease = await verificationContext.Diseases
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.DiseaseCode == new DiseaseCode("PILOT_RESP"));
        var profile = disease is null
            ? null
            : await verificationContext.DiseaseTraceProfiles
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.DiseaseId == disease.DiseaseId);

        Assert.NotNull(disease);
        Assert.NotNull(profile);
        Assert.Equal(1, await verificationContext.Facilities.CountAsync(x => x.FacilityCode == new FacilityCode("DEV-PHX")));
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddDbContext<KennelTraceDbContext>(options =>
            options.UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName)));

        return services.BuildServiceProvider();
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
