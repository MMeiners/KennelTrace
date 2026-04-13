using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Animals;
using KennelTrace.Domain.Features.Facilities;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KennelTrace.Web.Development;

internal static class DevelopmentDatabaseSetup
{
    private const string SeedFacilityCode = "DEV-PHX";
    private const string SeedRoomCode = "ROOM-A";
    private const string SeedHallwayCode = "HALL-1";
    private const string SeedKennelOneCode = "KEN-1";
    private const string SeedKennelTwoCode = "KEN-2";
    private const string SeedKennelThreeCode = "KEN-3";
    private const string SeedOverflowCode = "KEN-OVR";
    private const string SeedAnimalCode = "A-100";

    public static async Task ApplyDevelopmentDatabaseSetupAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KennelTraceDbContext>();

        await dbContext.Database.MigrateAsync();

        var existingFacility = await dbContext.Facilities
            .SingleOrDefaultAsync(x => x.FacilityCode == new FacilityCode(SeedFacilityCode));
        if (existingFacility is not null)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var facility = new Facility(
            new FacilityCode(SeedFacilityCode),
            "Development Shelter",
            "America/Phoenix",
            nowUtc,
            nowUtc,
            notes: "Development-only startup seed data.");

        dbContext.Facilities.Add(facility);
        await dbContext.SaveChangesAsync();

        var room = new Location(
            facility.FacilityId,
            LocationType.Room,
            new LocationCode(SeedRoomCode),
            "Room A",
            nowUtc,
            nowUtc,
            displayOrder: 1,
            notes: "Read-only room layout for local development.");

        var hallway = new Location(
            facility.FacilityId,
            LocationType.Hallway,
            new LocationCode(SeedHallwayCode),
            "Hallway 1",
            nowUtc,
            nowUtc,
            displayOrder: 2);

        dbContext.Locations.AddRange(room, hallway);
        await dbContext.SaveChangesAsync();

        var kennelOne = new Location(
            facility.FacilityId,
            LocationType.Kennel,
            new LocationCode(SeedKennelOneCode),
            "Kennel 1",
            nowUtc,
            nowUtc,
            room.LocationId,
            gridRow: 0,
            gridColumn: 0);

        var kennelTwo = new Location(
            facility.FacilityId,
            LocationType.Kennel,
            new LocationCode(SeedKennelTwoCode),
            "Kennel 2",
            nowUtc,
            nowUtc,
            room.LocationId,
            gridRow: 0,
            gridColumn: 1);

        var kennelThree = new Location(
            facility.FacilityId,
            LocationType.Kennel,
            new LocationCode(SeedKennelThreeCode),
            "Kennel 3",
            nowUtc,
            nowUtc,
            room.LocationId,
            gridRow: 0,
            gridColumn: 2);

        var overflowKennel = new Location(
            facility.FacilityId,
            LocationType.Kennel,
            new LocationCode(SeedOverflowCode),
            "Overflow Kennel",
            nowUtc,
            nowUtc,
            room.LocationId,
            notes: "Unplaced development kennel.");

        dbContext.Locations.AddRange(kennelOne, kennelTwo, kennelThree, overflowKennel);
        await dbContext.SaveChangesAsync();

        dbContext.LocationLinks.AddRange(
            new LocationLink(facility.FacilityId, room.LocationId, hallway.LocationId, LinkType.Connected, nowUtc, nowUtc, sourceReference: "development-seed"),
            new LocationLink(facility.FacilityId, hallway.LocationId, room.LocationId, LinkType.Connected, nowUtc, nowUtc, sourceReference: "development-seed"),
            new LocationLink(facility.FacilityId, kennelOne.LocationId, kennelTwo.LocationId, LinkType.AdjacentRight, nowUtc, nowUtc, sourceReference: "development-seed"),
            new LocationLink(facility.FacilityId, kennelTwo.LocationId, kennelOne.LocationId, LinkType.AdjacentLeft, nowUtc, nowUtc, sourceReference: "development-seed"),
            new LocationLink(facility.FacilityId, kennelTwo.LocationId, kennelThree.LocationId, LinkType.AdjacentRight, nowUtc, nowUtc, sourceReference: "development-seed"),
            new LocationLink(facility.FacilityId, kennelThree.LocationId, kennelTwo.LocationId, LinkType.AdjacentLeft, nowUtc, nowUtc, sourceReference: "development-seed"));

        var animal = new Animal(
            new AnimalCode(SeedAnimalCode),
            nowUtc,
            nowUtc,
            name: "Scout",
            notes: "Development-only current occupant.");

        dbContext.Animals.Add(animal);
        await dbContext.SaveChangesAsync();

        dbContext.MovementEvents.Add(new MovementEvent(
            animal.AnimalId,
            kennelOne.LocationId,
            nowUtc.AddHours(-6),
            nowUtc,
            nowUtc,
            notes: "Current placement seeded for local development."));

        await dbContext.SaveChangesAsync();
    }
}
