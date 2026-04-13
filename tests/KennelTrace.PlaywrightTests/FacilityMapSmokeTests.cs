using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace KennelTrace.PlaywrightTests;

public sealed class FacilityMapSmokeTests : IAsyncLifetime
{
    private readonly FacilityMapWebApplicationFactory _factory = new();
    private HttpClient? _client;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private Uri? _baseAddress;

    public async Task InitializeAsync()
    {
        _factory.UseKestrel(0);

        _client = _factory.CreateClient();
        _baseAddress = _client.BaseAddress;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task FacilityMap_HappyPath_Shows_Selected_Location_Details()
    {
        ArgumentNullException.ThrowIfNull(_browser);
        ArgumentNullException.ThrowIfNull(_baseAddress);

        var page = await _browser.NewPageAsync();
        await page.GotoAsync(new Uri(_baseAddress, "facility-map").ToString());
        await page.WaitForResponseAsync(
            response => response.Url.Contains("/_blazor/negotiate", StringComparison.OrdinalIgnoreCase) && response.Ok,
            new PageWaitForResponseOptions
            {
                Timeout = 15000
            });
        await page.WaitForTimeoutAsync(1000);

        var facilitySelect = page.GetByTestId("facility-select");
        var roomSelect = page.GetByTestId("room-select");
        var placedLocations = page.GetByTestId("placed-locations");
        var locationDetail = page.GetByTestId("location-detail");

        await Expect(facilitySelect).ToBeVisibleAsync();
        await facilitySelect.SelectOptionAsync("12");

        await Expect(roomSelect).ToContainTextAsync("Room A");
        await roomSelect.SelectOptionAsync("101");

        await Expect(placedLocations).ToBeVisibleAsync();
        await Expect(placedLocations).ToContainTextAsync("Kennel 1");

        await page.GetByTestId("location-201").ClickAsync();

        await Expect(locationDetail).ToContainTextAsync("Kennel 1");
        await Expect(locationDetail).ToContainTextAsync("AdjacentRight");
        await Expect(page.GetByTestId("location-detail-name")).ToHaveTextAsync("Kennel 1");
    }

    private sealed class FacilityMapWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFacilityMapReadService>();
                services.RemoveAll<FacilityMapReadService>();
                services.AddSingleton<IFacilityMapReadService>(new FakeFacilityMapReadService());
            });
        }
    }

    private sealed class FakeFacilityMapReadService : IFacilityMapReadService
    {
        private static readonly FacilityMapFacilityOption Facility = new(12, new FacilityCode("PHX"), "Phoenix Shelter", true);
        private static readonly FacilityMapRoomOption Room = new(12, 101, new LocationCode("ROOM-A"), "Room A", LocationType.Room, true);

        private static readonly RoomMapResult RoomMap = new(
            Facility,
            Location(101, "ROOM-A", "Room A", LocationType.Room),
            [
                Location(
                    201,
                    "KEN-1",
                    "Kennel 1",
                    LocationType.Kennel,
                    gridRow: 0,
                    gridColumn: 0,
                    occupancyCount: 1,
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
                    ]),
                Location(202, "KEN-2", "Kennel 2", LocationType.Kennel, gridRow: 0, gridColumn: 1)
            ],
            [Location(203, "KEN-3", "Overflow Kennel", LocationType.Kennel, occupancyCount: 2)]);

        public Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FacilityMapFacilityOption>>([Facility]);

        public Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FacilityMapRoomOption>>(facilityId == Facility.FacilityId ? [Room] : []);

        public Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<RoomMapResult?>(facilityId == Facility.FacilityId && roomLocationId == Room.RoomLocationId ? RoomMap : null);

        private static FacilityMapLocationDetail Location(
            int locationId,
            string locationCode,
            string name,
            LocationType locationType,
            int? gridRow = null,
            int? gridColumn = null,
            int occupancyCount = 0,
            IReadOnlyList<FacilityMapLocationLink>? links = null) =>
            new(
                locationId,
                Facility.FacilityId,
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
    }
}
