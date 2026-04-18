using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using KennelTrace.Web.Components.Pages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class FacilityMapPageTests : BunitContext
{
    private readonly FakeFacilityMapReadService _service = new();

    public FacilityMapPageTests()
    {
        Services.AddMudServices();
        Services.AddSingleton<IFacilityMapReadService>(_service);
    }

    [Fact]
    public void Page_Renders_Successfully()
    {
        var cut = Render<FacilityMap>();

        Assert.Contains("Facility Map", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='facility-select']"));
        Assert.NotNull(cut.Find("[data-testid='room-select']"));
    }

    [Fact]
    public void Facilities_Are_Loaded_Through_The_Read_Service()
    {
        _service.Facilities =
        [
            Facility(12, "PHX", "Phoenix Shelter"),
            Facility(34, "TUC", "Tucson Shelter")
        ];

        var cut = Render<FacilityMap>();

        Assert.Equal(1, _service.ListFacilitiesCallCount);
        var facilitySelect = cut.Find("[data-testid='facility-select']");
        Assert.Contains("Phoenix Shelter (PHX)", facilitySelect.InnerHtml);
        Assert.Contains("Tucson Shelter (TUC)", facilitySelect.InnerHtml);
    }

    [Fact]
    public void Selecting_A_Facility_Refreshes_Room_Choices()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] =
        [
            Room(12, 101, "ROOM-A", "Room A", LocationType.Room),
            Room(12, 102, "ISO-A", "Isolation A", LocationType.Isolation)
        ];

        var cut = Render<FacilityMap>();

        cut.Find("[data-testid='facility-select']").Change("12");

        Assert.Equal([12], _service.ListRoomsFacilityIds);
        var roomSelect = cut.Find("[data-testid='room-select']");
        Assert.Contains("Room A (ROOM-A)", roomSelect.InnerHtml);
        Assert.Contains("Isolation A (ISO-A)", roomSelect.InnerHtml);
    }

    [Fact]
    public void Selecting_A_Room_Loads_And_Displays_The_Room_Map()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room, occupancyCount: 1),
            [Location(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0, occupancyCount: 2)],
            []);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");

        Assert.Equal([(12, 101)], _service.GetRoomMapCalls);
        Assert.Contains("Read-only room layout", cut.Markup);
        var placedLocations = cut.Find("[data-testid='placed-locations']").TextContent;
        Assert.Contains("Kennel 1", placedLocations);
        Assert.Contains("KEN-1", placedLocations);
    }

    [Fact]
    public void Selecting_A_Location_Updates_The_Detail_Section()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [
                Location(
                    201,
                    12,
                    "KEN-1",
                    "Kennel 1",
                    LocationType.Kennel,
                    gridRow: 0,
                    gridColumn: 0,
                    occupancyCount: 2,
                    links:
                    [
                        new FacilityMapLocationLink(
                            501,
                            201,
                            new LocationCode("KEN-1"),
                            "Kennel 1",
                            202,
                            new LocationCode("KEN-2"),
                            "Kennel 2",
                            LinkType.AdjacentRight,
                            SourceType.Import,
                            "layout",
                            null)
                    ])
            ],
            []);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");

        cut.Find("[data-testid='location-201']").Click();

        var detail = cut.Find("[data-testid='location-detail']");
        Assert.Equal("Kennel 1", cut.Find("[data-testid='location-detail-name']").TextContent);
        Assert.Contains("Kennel 1", detail.TextContent);
        Assert.Contains("Kennel", detail.TextContent);
        Assert.Contains("2", detail.TextContent);
        Assert.Contains("AdjacentRight", detail.TextContent);
        Assert.Contains("Kennel 2 (KEN-2)", detail.TextContent);
    }

    [Fact]
    public void Selected_Location_Detail_Shows_Trace_Scope_Deep_Link()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [Location(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0)],
            []);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");
        cut.Find("[data-testid='location-201']").Click();

        var action = cut.Find("[data-testid='facility-map-trace-scope-action']");
        Assert.Equal("/trace?scopeLocationId=201", action.GetAttribute("href"));
    }

    [Fact]
    public void Query_String_Loads_Selected_Room_And_Location_For_Trace_Round_Trip()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [Location(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0)],
            []);

        var navigationManager = Services.GetRequiredService<NavigationManager>();
        navigationManager.NavigateTo("http://localhost/facility-map?facilityId=12&roomLocationId=101&selectedLocationId=201");

        var cut = Render<FacilityMap>();

        Assert.Equal([12], _service.ListRoomsFacilityIds);
        Assert.Equal([(12, 101)], _service.GetRoomMapCalls);
        Assert.Equal("Kennel 1", cut.Find("[data-testid='location-detail-name']").TextContent);
    }

    [Fact]
    public void Selecting_A_Location_Marks_It_As_Selected()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [Location(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0)],
            []);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");

        var locationButton = cut.Find("[data-testid='location-201']");
        locationButton.Click();

        locationButton = cut.Find("[data-testid='location-201']");
        Assert.Contains("selected", locationButton.ClassList);
    }

    [Fact]
    public void Empty_Room_Shows_An_Empty_State()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [],
            []);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");

        Assert.Contains("This room does not have any placed or unplaced locations yet.", cut.Find("[data-testid='empty-state']").TextContent);
    }

    [Fact]
    public void Unplaced_Locations_Render_Separately_When_Present()
    {
        _service.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _service.RoomsByFacilityId[12] = [Room(12, 101, "ROOM-A", "Room A", LocationType.Room)];
        _service.RoomMaps[(12, 101)] = RoomMap(
            Facility(12, "PHX", "Phoenix Shelter"),
            Location(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [Location(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0)],
            [Location(202, 12, "KEN-UNPLACED", "Overflow Kennel", LocationType.Kennel)]);

        var cut = Render<FacilityMap>();
        cut.Find("[data-testid='facility-select']").Change("12");
        cut.Find("[data-testid='room-select']").Change("101");

        var placedLocations = cut.Find("[data-testid='placed-locations']").TextContent;
        Assert.Contains("Kennel 1", placedLocations);
        Assert.Contains("KEN-1", placedLocations);

        var unplacedLocations = cut.Find("[data-testid='unplaced-locations']").TextContent;
        Assert.Contains("Overflow Kennel", unplacedLocations);
        Assert.Contains("KEN-UNPLACED", unplacedLocations);
    }

    private static FacilityMapFacilityOption Facility(int facilityId, string facilityCode, string name) =>
        new(facilityId, new FacilityCode(facilityCode), name, true);

    private static FacilityMapRoomOption Room(int facilityId, int roomLocationId, string roomCode, string roomName, LocationType roomType) =>
        new(facilityId, roomLocationId, new LocationCode(roomCode), roomName, roomType, true);

    private static FacilityMapLocationDetail Location(
        int locationId,
        int facilityId,
        string locationCode,
        string name,
        LocationType locationType,
        int? gridRow = null,
        int? gridColumn = null,
        int occupancyCount = 0,
        IReadOnlyList<FacilityMapLocationLink>? links = null) =>
        new(
            locationId,
            facilityId,
            101,
            locationType,
            new LocationCode(locationCode),
            name,
            true,
            gridRow,
            gridColumn,
            0,
            null,
            null,
            occupancyCount,
            links ?? []);

    private static RoomMapResult RoomMap(
        FacilityMapFacilityOption facility,
        FacilityMapLocationDetail room,
        IReadOnlyList<FacilityMapLocationDetail> placedLocations,
        IReadOnlyList<FacilityMapLocationDetail> unplacedLocations) =>
        new(facility, room, placedLocations, unplacedLocations);

    private sealed class FakeFacilityMapReadService : IFacilityMapReadService
    {
        public IReadOnlyList<FacilityMapFacilityOption> Facilities { get; set; } = [];

        public Dictionary<int, IReadOnlyList<FacilityMapRoomOption>> RoomsByFacilityId { get; } = [];

        public Dictionary<(int FacilityId, int RoomLocationId), RoomMapResult?> RoomMaps { get; } = [];

        public int ListFacilitiesCallCount { get; private set; }

        public List<int> ListRoomsFacilityIds { get; } = [];

        public List<(int FacilityId, int RoomLocationId)> GetRoomMapCalls { get; } = [];

        public Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default)
        {
            ListFacilitiesCallCount++;
            return Task.FromResult(Facilities);
        }

        public Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default)
        {
            ListRoomsFacilityIds.Add(facilityId);
            return Task.FromResult(RoomsByFacilityId.GetValueOrDefault(facilityId, Array.Empty<FacilityMapRoomOption>()));
        }

        public Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default)
        {
            GetRoomMapCalls.Add((facilityId, roomLocationId));
            return Task.FromResult(RoomMaps.GetValueOrDefault((facilityId, roomLocationId)));
        }
    }
}
