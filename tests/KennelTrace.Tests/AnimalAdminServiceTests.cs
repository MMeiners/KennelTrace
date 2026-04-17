using System.Security.Claims;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Features.Animals.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Tests;

public sealed class AnimalAdminServiceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"KennelTrace_AnimalAdmin_{Guid.NewGuid():N}";
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
    public async Task Admin_Can_Create_Animal()
    {
        await using var context = CreateContext();
        var service = CreateService(context);

        var result = await service.SaveAsync(
            new AnimalSaveRequest(
                AnimalId: null,
                AnimalNumber: "A-100",
                Name: "Biscuit",
                Species: "Dog",
                Sex: "Female",
                Breed: "Mix",
                DateOfBirth: new DateOnly(2024, 1, 2),
                IsActive: true,
                Notes: "Friendly"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(AnimalSaveStatus.Success, result.Status);
        Assert.NotNull(result.Animal);

        await using var verificationContext = CreateContext();
        var animal = await verificationContext.Animals.SingleAsync();

        Assert.Equal("A-100", animal.AnimalNumber.Value);
        Assert.Equal("Biscuit", animal.Name);
        Assert.Equal("Dog", animal.Species);
        Assert.Equal("Female", animal.Sex);
        Assert.Equal("Mix", animal.Breed);
        Assert.Equal(new DateOnly(2024, 1, 2), animal.DateOfBirth);
        Assert.True(animal.IsActive);
        Assert.Equal("Friendly", animal.Notes);
    }

    [Fact]
    public async Task Admin_Can_Update_Animal()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 12);
        var animal = new Animal(new AnimalCode("A-100"), now, now, name: "Biscuit", species: "Dog");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.SaveAsync(
            new AnimalSaveRequest(
                animal.AnimalId,
                "A-101",
                "Scout",
                "Dog",
                "Male",
                "Shepherd Mix",
                new DateOnly(2023, 5, 6),
                true,
                "Updated"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(AnimalSaveStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var updatedAnimal = await verificationContext.Animals.SingleAsync();

        Assert.Equal("A-101", updatedAnimal.AnimalNumber.Value);
        Assert.Equal("Scout", updatedAnimal.Name);
        Assert.Equal("Male", updatedAnimal.Sex);
        Assert.Equal("Shepherd Mix", updatedAnimal.Breed);
        Assert.Equal(new DateOnly(2023, 5, 6), updatedAnimal.DateOfBirth);
        Assert.Equal("Updated", updatedAnimal.Notes);
        Assert.True(updatedAnimal.ModifiedUtc > now);
    }

    [Fact]
    public async Task Animal_Number_Must_Be_Unique()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 12);
        context.Animals.Add(new Animal(new AnimalCode("A-100"), now, now, name: "Existing"));
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.SaveAsync(
            new AnimalSaveRequest(
                null,
                "A-100",
                "Duplicate",
                "Dog",
                null,
                null,
                null,
                true,
                null),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(AnimalSaveStatus.ValidationFailed, result.Status);
        Assert.Contains("Animal number must be unique.", result.ValidationErrors[nameof(AnimalSaveRequest.AnimalNumber)]);

        await using var verificationContext = CreateContext();
        Assert.Equal(1, await verificationContext.Animals.CountAsync());
    }

    [Fact]
    public async Task Deactivate_Workflow_Updates_IsActive_Instead_Of_Deleting()
    {
        await using var context = CreateContext();
        var now = Utc(2026, 4, 17, 12);
        var animal = new Animal(new AnimalCode("A-100"), now, now, name: "Biscuit");
        context.Animals.Add(animal);
        await context.SaveChangesAsync();

        var service = CreateService(context);

        var result = await service.SaveAsync(
            new AnimalSaveRequest(
                animal.AnimalId,
                "A-100",
                "Biscuit",
                "Dog",
                null,
                null,
                null,
                false,
                "Inactive record retained"),
            CreateUser(KennelTraceRoles.Admin));

        Assert.Equal(AnimalSaveStatus.Success, result.Status);

        await using var verificationContext = CreateContext();
        var animals = await verificationContext.Animals.ToListAsync();

        Assert.Single(animals);
        Assert.False(animals[0].IsActive);
        Assert.Equal("Inactive record retained", animals[0].Notes);
    }

    private KennelTraceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<KennelTraceDbContext>()
            .UseSqlServer(_connectionString, sql => sql.MigrationsAssembly(typeof(KennelTraceDbContext).Assembly.FullName))
            .Options;

        return new KennelTraceDbContext(options);
    }

    private static AnimalAdminService CreateService(KennelTraceDbContext context) =>
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
