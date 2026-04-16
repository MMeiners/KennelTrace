using System.Security.Claims;
using System.Text.Encodings.Web;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using KennelTrace.Web.Features.Facilities.Admin;
using KennelTrace.Web.Features.Locations.Admin;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace KennelTrace.PlaywrightTests;

public sealed class AdminLayoutSmokeTests : IAsyncLifetime
{
    private readonly AdminLayoutWebApplicationFactory _factory = new();
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
    public async Task Admin_Layout_HappyPath_Creates_Room_And_Kennel_Then_Shows_Them_On_Facility_Map()
    {
        ArgumentNullException.ThrowIfNull(_browser);
        ArgumentNullException.ThrowIfNull(_baseAddress);

        var page = await _browser.NewPageAsync();
        await page.GotoAsync(new Uri(_baseAddress, "admin/layout").ToString());
        await page.WaitForResponseAsync(
            response => response.Url.Contains("/_blazor/negotiate", StringComparison.OrdinalIgnoreCase) && response.Ok,
            new PageWaitForResponseOptions
            {
                Timeout = 15000
            });

        await Expect(page.GetByTestId("admin-layout-page")).ToHaveTextAsync("Layout Admin");

        await page.GetByTestId("location-code-input").FillAsync("ROOM-B");
        await page.GetByTestId("location-name-input").FillAsync("Surgery Prep");
        await page.GetByTestId("location-save-button").ClickAsync();

        await Expect(page.GetByTestId("location-save-success")).ToContainTextAsync("Surgery Prep saved.");
        await Expect(page.GetByTestId("location-item-200")).ToContainTextAsync("Surgery Prep");

        await page.GetByTestId("new-child-location-button").ClickAsync();
        await Expect(page.GetByTestId("location-type-input")).ToHaveValueAsync(LocationType.Kennel.ToString());

        await page.GetByTestId("location-code-input").FillAsync("KEN-B1");
        await page.GetByTestId("location-name-input").FillAsync("Prep Kennel 1");
        await page.GetByTestId("location-save-button").ClickAsync();

        await Expect(page.GetByTestId("location-save-success")).ToContainTextAsync("Prep Kennel 1 saved.");
        await Expect(page.GetByTestId("location-item-201")).ToContainTextAsync("Prep Kennel 1");

        await page.GetByTestId("location-item-200").ClickAsync();
        await page.GetByTestId("placement-grid-row-201").FillAsync("0");
        await page.GetByTestId("placement-grid-column-201").FillAsync("0");
        await page.GetByTestId("placement-stack-level-201").FillAsync("0");
        await page.GetByTestId("placement-display-order-201").FillAsync("1");
        await page.GetByTestId("placement-save-201").ClickAsync();

        await Expect(page.GetByTestId("room-placement-success")).ToContainTextAsync("Prep Kennel 1 placement saved for Surgery Prep.");

        await page.GetByTestId("add-outgoing-link-button").ClickAsync();
        await page.GetByTestId("link-type-input").SelectOptionAsync(LinkType.Connected.ToString());
        await page.GetByTestId("link-counterparty-input").SelectOptionAsync("110");
        await page.GetByTestId("link-notes-input").FillAsync("Smoke test topology link");
        await page.GetByTestId("link-save-button").ClickAsync();

        await Expect(page.GetByTestId("link-save-success")).ToContainTextAsync("Connected saved for Surgery Prep.");

        await page.GotoAsync(new Uri(_baseAddress, "facility-map").ToString());
        await Expect(page.GetByTestId("facility-select")).ToBeVisibleAsync();
        await page.GetByTestId("facility-select").SelectOptionAsync("12");
        await Expect(page.GetByTestId("room-select")).ToContainTextAsync("Surgery Prep");
        await page.GetByTestId("room-select").SelectOptionAsync("200");

        await Expect(page.GetByTestId("location-detail")).ToContainTextAsync("Surgery Prep");
        await Expect(page.GetByTestId("location-detail")).ToContainTextAsync("Connected");
        await Expect(page.GetByTestId("location-detail")).ToContainTextAsync("Main Hallway");
        await Expect(page.GetByTestId("placed-locations")).ToContainTextAsync("Prep Kennel 1");

        await page.GetByTestId("location-201").ClickAsync();
        await Expect(page.GetByTestId("location-detail-name")).ToHaveTextAsync("Prep Kennel 1");
    }

    private sealed class AdminLayoutWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IFacilityAdminService>();
                services.RemoveAll<ILocationAdminService>();
                services.RemoveAll<ILocationLinkAdminService>();
                services.RemoveAll<IFacilityMapReadService>();

                services.AddSingleton<LayoutTestStore>();
                services.AddSingleton<IFacilityAdminService, TestFacilityAdminService>();
                services.AddSingleton<ILocationAdminService, TestLocationAdminService>();
                services.AddSingleton<ILocationLinkAdminService, TestLocationLinkAdminService>();
                services.AddSingleton<IFacilityMapReadService, TestFacilityMapReadService>();

                services.AddAuthentication(options =>
                    {
                        options.DefaultScheme = TestAuthHandler.SchemeName;
                        options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                        options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });
            });
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PlaywrightAdmin";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "playwright-admin"),
                new Claim(ClaimTypes.Name, "playwright-admin"),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "ReadOnly")
            };

            var identity = new ClaimsIdentity(claims, SchemeName);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed class LayoutTestStore
    {
        private readonly List<FacilityState> _facilities =
        [
            new FacilityState(12, "PHX", "Phoenix Shelter", "America/Phoenix", true, null)
        ];

        private readonly List<LocationState> _locations =
        [
            new(110, 12, null, LocationType.Hallway, "HALL-1", "Main Hallway", true, null, null, 0, 0, null),
            new(120, 12, null, LocationType.Room, "ROOM-A", "Existing Room", true, null, null, 0, 0, null)
        ];

        private readonly List<LinkState> _links = [];
        private int _nextLocationId = 200;
        private int _nextLinkId = 500;

        public IReadOnlyList<FacilityAdminListItem> ListFacilities() =>
            _facilities
                .Select(facility => new FacilityAdminListItem(
                    facility.FacilityId,
                    facility.FacilityCode,
                    facility.Name,
                    facility.TimeZoneId,
                    facility.IsActive,
                    facility.Notes,
                    DateTime.UtcNow,
                    DateTime.UtcNow))
                .ToList();

        public FacilitySaveResult SaveFacility(FacilitySaveRequest request)
        {
            if (request.FacilityId is null)
            {
                return FacilitySaveResult.Forbidden();
            }

            var facility = _facilities.Single(x => x.FacilityId == request.FacilityId.Value);
            facility.FacilityCode = request.FacilityCode.Trim();
            facility.Name = request.Name.Trim();
            facility.TimeZoneId = request.TimeZoneId.Trim();
            facility.IsActive = request.IsActive;
            facility.Notes = request.Notes;

            return FacilitySaveResult.Success(new FacilityAdminListItem(
                facility.FacilityId,
                facility.FacilityCode,
                facility.Name,
                facility.TimeZoneId,
                facility.IsActive,
                facility.Notes,
                DateTime.UtcNow,
                DateTime.UtcNow));
        }

        public LocationAdminFacilityView GetFacilityView(int facilityId)
        {
            var facility = _facilities.Single(x => x.FacilityId == facilityId);
            var locations = _locations
                .Where(x => x.FacilityId == facilityId)
                .OrderBy(x => x.ParentLocationId.HasValue ? 1 : 0)
                .ThenBy(x => x.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.LocationCode)
                .Select(x => new LocationAdminListItem(
                    x.LocationId,
                    x.FacilityId,
                    x.ParentLocationId,
                    x.LocationType,
                    x.LocationCode,
                    x.Name,
                    x.GridRow,
                    x.GridColumn,
                    x.StackLevel,
                    x.DisplayOrder,
                    x.IsActive,
                    x.Notes))
                .ToList();

            var links = _links
                .Where(x => x.FacilityId == facilityId && x.IsActive)
                .OrderBy(x => x.LinkType)
                .ThenBy(x => x.FromLocationCode)
                .ThenBy(x => x.ToLocationCode)
                .Select(x => new LocationAdminLinkListItem(
                    x.LocationLinkId,
                    x.FacilityId,
                    x.FromLocationId,
                    x.FromLocationCode,
                    x.FromLocationName,
                    x.FromLocationType,
                    x.ToLocationId,
                    x.ToLocationCode,
                    x.ToLocationName,
                    x.ToLocationType,
                    x.LinkType,
                    SourceType.Manual,
                    x.SourceReference,
                    x.Notes))
                .ToList();

            return new LocationAdminFacilityView(
                facility.FacilityId,
                facility.FacilityCode,
                facility.Name,
                facility.IsActive,
                locations,
                links,
                BuildTree(locations, null));
        }

        public LocationSaveResult SaveLocation(LocationSaveRequest request)
        {
            if (request.LocationId is null)
            {
                var locationId = _nextLocationId++;
                var created = new LocationState(
                    locationId,
                    request.FacilityId,
                    request.ParentLocationId,
                    request.LocationType,
                    request.LocationCode.Trim(),
                    request.Name.Trim(),
                    request.IsActive,
                    request.GridRow,
                    request.GridColumn,
                    request.StackLevel,
                    request.DisplayOrder,
                    request.Notes);

                _locations.Add(created);
                return LocationSaveResult.Success(ToAdminItem(created));
            }

            var existing = _locations.Single(x => x.LocationId == request.LocationId.Value && x.FacilityId == request.FacilityId);
            existing.ParentLocationId = request.ParentLocationId;
            existing.LocationType = request.LocationType;
            existing.LocationCode = request.LocationCode.Trim();
            existing.Name = request.Name.Trim();
            existing.IsActive = request.IsActive;
            existing.GridRow = request.GridRow;
            existing.GridColumn = request.GridColumn;
            existing.StackLevel = request.StackLevel;
            existing.DisplayOrder = request.DisplayOrder;
            existing.Notes = request.Notes;

            return LocationSaveResult.Success(ToAdminItem(existing));
        }

        public LocationLinkSaveResult SaveLink(LocationLinkSaveRequest request)
        {
            var fromLocation = _locations.Single(x => x.LocationId == request.FromLocationId && x.FacilityId == request.FacilityId);
            var toLocation = _locations.Single(x => x.LocationId == request.ToLocationId && x.FacilityId == request.FacilityId);

            UpsertLink(request.FacilityId, fromLocation, toLocation, request.LinkType, request.SourceReference, request.Notes);
            UpsertLink(request.FacilityId, toLocation, fromLocation, LinkTypeRules.InverseOf(request.LinkType), request.SourceReference, request.Notes);

            return LocationLinkSaveResult.Success();
        }

        public LocationLinkRemoveResult RemoveLink(LocationLinkRemoveRequest request)
        {
            foreach (var link in _links.Where(x =>
                         x.FacilityId == request.FacilityId
                         && ((x.FromLocationId == request.FromLocationId && x.ToLocationId == request.ToLocationId && x.LinkType == request.LinkType)
                             || (x.FromLocationId == request.ToLocationId && x.ToLocationId == request.FromLocationId && x.LinkType == LinkTypeRules.InverseOf(request.LinkType)))))
            {
                link.IsActive = false;
            }

            return LocationLinkRemoveResult.Success();
        }

        public IReadOnlyList<FacilityMapFacilityOption> ListMapFacilities() =>
            _facilities
                .OrderBy(x => x.Name)
                .Select(x => new FacilityMapFacilityOption(x.FacilityId, new FacilityCode(x.FacilityCode), x.Name, x.IsActive))
                .ToList();

        public IReadOnlyList<FacilityMapRoomOption> ListMapRooms(int facilityId) =>
            _locations
                .Where(x => x.FacilityId == facilityId && IsRoomLike(x.LocationType))
                .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.Name)
                .ThenBy(x => x.LocationCode)
                .Select(x => new FacilityMapRoomOption(
                    x.FacilityId,
                    x.LocationId,
                    new LocationCode(x.LocationCode),
                    x.Name,
                    x.LocationType,
                    x.IsActive))
                .ToList();

        public RoomMapResult? GetRoomMap(int facilityId, int roomLocationId)
        {
            var facility = _facilities.SingleOrDefault(x => x.FacilityId == facilityId);
            var room = _locations.SingleOrDefault(x => x.LocationId == roomLocationId && x.FacilityId == facilityId && IsRoomLike(x.LocationType));
            if (facility is null || room is null)
            {
                return null;
            }

            var facilityOption = new FacilityMapFacilityOption(facility.FacilityId, new FacilityCode(facility.FacilityCode), facility.Name, facility.IsActive);
            var roomDetail = ToFacilityMapLocation(room);
            var childLocations = _locations.Where(x => x.ParentLocationId == roomLocationId && x.FacilityId == facilityId).ToList();

            var placed = childLocations
                .Where(x => x.GridRow.HasValue && x.GridColumn.HasValue)
                .OrderBy(x => x.GridRow)
                .ThenBy(x => x.GridColumn)
                .ThenBy(x => x.StackLevel)
                .ThenBy(x => x.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.Name)
                .Select(ToFacilityMapLocation)
                .ToList();

            var unplaced = childLocations
                .Where(x => !x.GridRow.HasValue || !x.GridColumn.HasValue)
                .OrderBy(x => x.DisplayOrder ?? int.MaxValue)
                .ThenBy(x => x.Name)
                .Select(ToFacilityMapLocation)
                .ToList();

            return new RoomMapResult(facilityOption, roomDetail, placed, unplaced);
        }

        private void UpsertLink(int facilityId, LocationState fromLocation, LocationState toLocation, LinkType linkType, string? sourceReference, string? notes)
        {
            var existing = _links.SingleOrDefault(x =>
                x.FacilityId == facilityId
                && x.FromLocationId == fromLocation.LocationId
                && x.ToLocationId == toLocation.LocationId
                && x.LinkType == linkType);

            if (existing is null)
            {
                _links.Add(new LinkState(
                    _nextLinkId++,
                    facilityId,
                    fromLocation.LocationId,
                    fromLocation.LocationCode,
                    fromLocation.Name,
                    fromLocation.LocationType,
                    toLocation.LocationId,
                    toLocation.LocationCode,
                    toLocation.Name,
                    toLocation.LocationType,
                    linkType,
                    true,
                    sourceReference,
                    notes));
                return;
            }

            existing.IsActive = true;
            existing.SourceReference = sourceReference;
            existing.Notes = notes;
        }

        private FacilityMapLocationDetail ToFacilityMapLocation(LocationState location)
        {
            var links = _links
                .Where(x => x.IsActive && (x.FromLocationId == location.LocationId || x.ToLocationId == location.LocationId))
                .OrderBy(x => x.LinkType)
                .ThenBy(x => x.FromLocationCode)
                .ThenBy(x => x.ToLocationCode)
                .Select(x => new FacilityMapLocationLink(
                    x.LocationLinkId,
                    x.FromLocationId,
                    new LocationCode(x.FromLocationCode),
                    x.FromLocationName,
                    x.ToLocationId,
                    new LocationCode(x.ToLocationCode),
                    x.ToLocationName,
                    x.LinkType,
                    SourceType.Manual,
                    x.SourceReference,
                    x.Notes))
                .ToList();

            return new FacilityMapLocationDetail(
                location.LocationId,
                location.FacilityId,
                location.ParentLocationId,
                location.LocationType,
                new LocationCode(location.LocationCode),
                location.Name,
                location.IsActive,
                location.GridRow,
                location.GridColumn,
                location.StackLevel,
                location.DisplayOrder,
                location.Notes,
                0,
                links);
        }

        private static IReadOnlyList<LocationAdminTreeItem> BuildTree(IReadOnlyList<LocationAdminListItem> locations, int? parentLocationId) =>
            locations
                .Where(location => location.ParentLocationId == parentLocationId)
                .OrderBy(location => location.DisplayOrder ?? int.MaxValue)
                .ThenBy(location => location.Name)
                .ThenBy(location => location.LocationCode)
                .Select(location => new LocationAdminTreeItem(
                    location.LocationId,
                    location.FacilityId,
                    location.ParentLocationId,
                    location.LocationType,
                    location.LocationCode,
                    location.Name,
                    location.DisplayOrder,
                    location.IsActive,
                    location.Notes,
                    BuildTree(locations, location.LocationId)))
                .ToList();

        private static LocationAdminListItem ToAdminItem(LocationState location) =>
            new(
                location.LocationId,
                location.FacilityId,
                location.ParentLocationId,
                location.LocationType,
                location.LocationCode,
                location.Name,
                location.GridRow,
                location.GridColumn,
                location.StackLevel,
                location.DisplayOrder,
                location.IsActive,
                location.Notes);

        private static bool IsRoomLike(LocationType locationType) =>
            locationType is LocationType.Room or LocationType.Medical or LocationType.Isolation or LocationType.Intake;

        private sealed class FacilityState(
            int facilityId,
            string facilityCode,
            string name,
            string timeZoneId,
            bool isActive,
            string? notes)
        {
            public int FacilityId { get; } = facilityId;
            public string FacilityCode { get; set; } = facilityCode;
            public string Name { get; set; } = name;
            public string TimeZoneId { get; set; } = timeZoneId;
            public bool IsActive { get; set; } = isActive;
            public string? Notes { get; set; } = notes;
        }

        private sealed class LocationState(
            int locationId,
            int facilityId,
            int? parentLocationId,
            LocationType locationType,
            string locationCode,
            string name,
            bool isActive,
            int? gridRow,
            int? gridColumn,
            int stackLevel,
            int? displayOrder,
            string? notes)
        {
            public int LocationId { get; } = locationId;
            public int FacilityId { get; } = facilityId;
            public int? ParentLocationId { get; set; } = parentLocationId;
            public LocationType LocationType { get; set; } = locationType;
            public string LocationCode { get; set; } = locationCode;
            public string Name { get; set; } = name;
            public bool IsActive { get; set; } = isActive;
            public int? GridRow { get; set; } = gridRow;
            public int? GridColumn { get; set; } = gridColumn;
            public int StackLevel { get; set; } = stackLevel;
            public int? DisplayOrder { get; set; } = displayOrder;
            public string? Notes { get; set; } = notes;
        }

        private sealed class LinkState(
            int locationLinkId,
            int facilityId,
            int fromLocationId,
            string fromLocationCode,
            string fromLocationName,
            LocationType fromLocationType,
            int toLocationId,
            string toLocationCode,
            string toLocationName,
            LocationType toLocationType,
            LinkType linkType,
            bool isActive,
            string? sourceReference,
            string? notes)
        {
            public int LocationLinkId { get; } = locationLinkId;
            public int FacilityId { get; } = facilityId;
            public int FromLocationId { get; } = fromLocationId;
            public string FromLocationCode { get; } = fromLocationCode;
            public string FromLocationName { get; } = fromLocationName;
            public LocationType FromLocationType { get; } = fromLocationType;
            public int ToLocationId { get; } = toLocationId;
            public string ToLocationCode { get; } = toLocationCode;
            public string ToLocationName { get; } = toLocationName;
            public LocationType ToLocationType { get; } = toLocationType;
            public LinkType LinkType { get; } = linkType;
            public bool IsActive { get; set; } = isActive;
            public string? SourceReference { get; set; } = sourceReference;
            public string? Notes { get; set; } = notes;
        }
    }

    private sealed class TestFacilityAdminService(LayoutTestStore store) : IFacilityAdminService
    {
        public Task<IReadOnlyList<FacilityAdminListItem>> ListFacilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FacilityAdminListItem>>(store.ListFacilities());

        public Task<FacilitySaveResult> SaveAsync(FacilitySaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.SaveFacility(request));
    }

    private sealed class TestLocationAdminService(LayoutTestStore store) : ILocationAdminService
    {
        public Task<LocationAdminFacilityView?> GetFacilityAsync(int facilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<LocationAdminFacilityView?>(store.GetFacilityView(facilityId));

        public Task<LocationSaveResult> SaveAsync(LocationSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.SaveLocation(request));
    }

    private sealed class TestLocationLinkAdminService(LayoutTestStore store) : ILocationLinkAdminService
    {
        public Task<LocationLinkSaveResult> SaveAsync(LocationLinkSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.SaveLink(request));

        public Task<LocationLinkRemoveResult> RemoveAsync(LocationLinkRemoveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.RemoveLink(request));
    }

    private sealed class TestFacilityMapReadService(LayoutTestStore store) : IFacilityMapReadService
    {
        public Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(store.ListMapFacilities());

        public Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.ListMapRooms(facilityId));

        public Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(store.GetRoomMap(facilityId, roomLocationId));
    }
}
