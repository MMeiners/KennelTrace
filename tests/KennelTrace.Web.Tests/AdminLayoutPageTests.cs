using System.Security.Claims;
using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Facilities.FacilityMap;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Facilities.Admin;
using KennelTrace.Web.Features.Locations.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class AdminLayoutPageTests : BunitContext
{
    private readonly FakeFacilityAdminService _facilityService = new();
    private readonly FakeFacilityMapReadService _facilityMapReadService = new();
    private readonly FakeLocationAdminService _locationService = new();
    private readonly FakeLocationLinkAdminService _locationLinkService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminLayoutPageTests()
    {
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IFacilityAdminService>(_facilityService);
        Services.AddSingleton<IFacilityMapReadService>(_facilityMapReadService);
        Services.AddSingleton<ILocationAdminService>(_locationService);
        Services.AddSingleton<ILocationLinkAdminService>(_locationLinkService);

        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);
    }

    [Fact]
    public void Facility_Switching_Loads_The_Selected_Facility_Structure()
    {
        _facilityService.Facilities =
        [
            Facility(12, "PHX", "Phoenix Shelter"),
            Facility(34, "TUC", "Tucson Shelter")
        ];
        _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", [Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A")]);
        _locationService.FacilityViews[34] = View(34, "TUC", "Tucson Shelter", [Location(201, 34, null, LocationType.Room, "ROOM-B", "Room B")]);

        var cut = Render<AdminLayout>();

        Assert.Equal([12], _locationService.GetFacilityCalls);
        Assert.Contains("Room A", cut.Markup);

        cut.Find("[data-testid='location-facility-select']").Change("34");

        Assert.Equal([12, 34], _locationService.GetFacilityCalls);
        Assert.Contains("Room B", cut.Markup);
    }

    [Fact]
    public void Selecting_A_Location_Populates_The_Edit_Form()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A", displayOrder: 1),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1", gridRow: 0, gridColumn: 1, stackLevel: 2, notes: "Window side")
            ]);

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-201']").Click();

        Assert.Equal("KEN-1", cut.Find("[data-testid='location-code-input']").GetAttribute("value"));
        Assert.Equal("Kennel 1", cut.Find("[data-testid='location-name-input']").GetAttribute("value"));
        Assert.Equal("0", cut.Find("[data-testid='grid-row-input']").GetAttribute("value"));
        Assert.Equal("1", cut.Find("[data-testid='grid-column-input']").GetAttribute("value"));
        Assert.Equal("2", cut.Find("[data-testid='stack-level-input']").GetAttribute("value"));
        Assert.Contains("Edit Location", cut.Find("[data-testid='location-form']").TextContent);
    }

    [Fact]
    public void Create_Flow_Saves_And_Refreshes_The_Browser()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", []);
        _locationService.OnSave = request =>
        {
            var createdLocation = Location(301, 12, null, request.LocationType, request.LocationCode, request.Name, request.GridRow, request.GridColumn, request.StackLevel, request.DisplayOrder, request.IsActive, request.Notes);
            _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", [createdLocation]);
            return LocationSaveResult.Success(createdLocation);
        };

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-code-input']").Change("ROOM-C");
        cut.Find("[data-testid='location-name-input']").Change("Room C");
        cut.Find("[data-testid='display-order-input']").Change("5");
        cut.Find("[data-testid='location-save-button']").Click();

        Assert.Single(_locationService.SaveRequests);
        Assert.Equal("ROOM-C", _locationService.SaveRequests[0].LocationCode);
        Assert.Contains("Room C", cut.Markup);
        Assert.Equal("ROOM-C", cut.Find("[data-testid='location-code-input']").GetAttribute("value"));
        Assert.Contains("Room C saved.", cut.Find("[data-testid='location-save-success']").TextContent);
    }

    [Fact]
    public void Edit_Flow_Saves_The_Selected_Location()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A", displayOrder: 1)]);
        _locationService.OnSave = request =>
        {
            var updatedLocation = Location(101, 12, null, request.LocationType, request.LocationCode, request.Name, request.GridRow, request.GridColumn, request.StackLevel, request.DisplayOrder, request.IsActive, request.Notes);
            _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", [updatedLocation]);
            return LocationSaveResult.Success(updatedLocation);
        };

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-101']").Click();
        cut.Find("[data-testid='location-name-input']").Change("Room Alpha");
        cut.Find("[data-testid='location-save-button']").Click();

        Assert.Single(_locationService.SaveRequests);
        Assert.Equal(101, _locationService.SaveRequests[0].LocationId);
        Assert.Equal("Room Alpha", _locationService.SaveRequests[0].Name);
        Assert.Equal("Room Alpha", cut.Find("[data-testid='location-name-input']").GetAttribute("value"));
    }

    [Fact]
    public void Validation_Message_Is_Displayed_For_Location_Save()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", []);
        _locationService.OnSave = _ => LocationSaveResult.ValidationFailed(new Dictionary<string, string[]>
        {
            [nameof(LocationSaveRequest.LocationCode)] = ["Location code must be unique within the facility."]
        });

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-code-input']").Change("ROOM-A");
        cut.Find("[data-testid='location-name-input']").Change("Room A");
        cut.Find("[data-testid='location-save-button']").Click();

        Assert.Contains("Location code must be unique within the facility.", cut.Markup);
    }

    [Fact]
    public void Selected_Room_Shows_Kennel_Placement_Table_And_Stored_Preview()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1", gridRow: 0, gridColumn: 0),
                Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
            ]);
        _facilityMapReadService.RoomMaps[(12, 101)] = RoomMap(
            FacilityMapFacility(12, "PHX", "Phoenix Shelter"),
            FacilityMapLocation(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [FacilityMapLocation(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, gridRow: 0, gridColumn: 0)],
            [FacilityMapLocation(202, 12, "KEN-2", "Kennel 2", LocationType.Kennel)]);

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-101']").Click();

        Assert.Contains("Kennel Placement", cut.Find("[data-testid='room-placement-editor']").TextContent);
        Assert.Contains("Kennel 1", cut.Find("[data-testid='room-placement-table']").TextContent);
        Assert.Contains("Kennel 1", cut.Find("[data-testid='admin-placed-preview']").TextContent);
        Assert.Contains("Kennel 2", cut.Find("[data-testid='admin-unplaced-preview']").TextContent);
    }

    [Fact]
    public void Room_Placement_Row_Saves_Kennel_Grid_Fields()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1")
            ]);
        _facilityMapReadService.RoomMaps[(12, 101)] = RoomMap(
            FacilityMapFacility(12, "PHX", "Phoenix Shelter"),
            FacilityMapLocation(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [],
            [FacilityMapLocation(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel)]);
        _locationService.OnSave = request =>
        {
            var updatedLocation = Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1", request.GridRow, request.GridColumn, request.StackLevel, request.DisplayOrder, request.IsActive, request.Notes);
            _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", [Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"), updatedLocation]);
            _facilityMapReadService.RoomMaps[(12, 101)] = RoomMap(
                FacilityMapFacility(12, "PHX", "Phoenix Shelter"),
                FacilityMapLocation(101, 12, "ROOM-A", "Room A", LocationType.Room),
                [FacilityMapLocation(201, 12, "KEN-1", "Kennel 1", LocationType.Kennel, request.GridRow, request.GridColumn, request.StackLevel, request.DisplayOrder)],
                []);
            return LocationSaveResult.Success(updatedLocation);
        };

        var cut = Render<AdminLayout>();
        cut.Find("[data-testid='location-item-101']").Click();

        cut.Find("[data-testid='placement-grid-row-201']").Change("2");
        cut.Find("[data-testid='placement-grid-column-201']").Change("3");
        cut.Find("[data-testid='placement-stack-level-201']").Change("1");
        cut.Find("[data-testid='placement-display-order-201']").Change("7");
        cut.Find("[data-testid='placement-save-201']").Click();

        Assert.Single(_locationService.SaveRequests);
        Assert.Equal(2, _locationService.SaveRequests[0].GridRow);
        Assert.Equal(3, _locationService.SaveRequests[0].GridColumn);
        Assert.Equal(1, _locationService.SaveRequests[0].StackLevel);
        Assert.Equal(7, _locationService.SaveRequests[0].DisplayOrder);
        Assert.Contains("Kennel 1 placement saved for Room A.", cut.Find("[data-testid='room-placement-success']").TextContent);
    }

    [Fact]
    public void Selected_Location_Shows_Outgoing_And_Incoming_Link_Tables()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1"),
                Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
            ],
            [
                Link(501, 12, 201, "KEN-1", "Kennel 1", LocationType.Kennel, 202, "KEN-2", "Kennel 2", LocationType.Kennel, LinkType.AdjacentRight, SourceType.Manual, "diagram", "Window side"),
                Link(502, 12, 202, "KEN-2", "Kennel 2", LocationType.Kennel, 201, "KEN-1", "Kennel 1", LocationType.Kennel, LinkType.AdjacentLeft, SourceType.Manual, "diagram", "Window side")
            ]);

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-201']").Click();

        Assert.Contains("AdjacentRight", cut.Find("[data-testid='outgoing-links-table']").TextContent);
        Assert.Contains("Kennel 2", cut.Find("[data-testid='outgoing-links-table']").TextContent);
        Assert.Contains("AdjacentLeft", cut.Find("[data-testid='incoming-links-table']").TextContent);
        Assert.Contains("Kennel 2", cut.Find("[data-testid='incoming-links-table']").TextContent);
    }

    [Fact]
    public void Add_Link_Dialog_Saves_And_Refreshes_The_Selected_Location()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1"),
                Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
            ]);
        _locationLinkService.OnSave = request =>
        {
            _locationService.FacilityViews[12] = View(
                12,
                "PHX",
                "Phoenix Shelter",
                [
                    Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                    Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1"),
                    Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
                ],
                [
                    Link(601, 12, request.FromLocationId, "KEN-1", "Kennel 1", LocationType.Kennel, request.ToLocationId, "KEN-2", "Kennel 2", LocationType.Kennel, request.LinkType, SourceType.Manual, request.SourceReference, request.Notes),
                    Link(602, 12, request.ToLocationId, "KEN-2", "Kennel 2", LocationType.Kennel, request.FromLocationId, "KEN-1", "Kennel 1", LocationType.Kennel, LinkType.AdjacentLeft, SourceType.Manual, request.SourceReference, request.Notes)
                ]);
            return LocationLinkSaveResult.Success();
        };

        var cut = Render<AdminLayout>();
        cut.Find("[data-testid='location-item-201']").Click();
        cut.Find("[data-testid='add-outgoing-link-button']").Click();

        cut.Find("[data-testid='link-type-input']").Change(LinkType.AdjacentRight.ToString());
        cut.Find("[data-testid='link-counterparty-input']").Change("202");
        cut.Find("[data-testid='link-source-reference-input']").Change("whiteboard");
        cut.Find("[data-testid='link-notes-input']").Change("Created during review");
        cut.Find("[data-testid='link-save-button']").Click();

        Assert.Single(_locationLinkService.SaveRequests);
        Assert.Equal(201, _locationLinkService.SaveRequests[0].FromLocationId);
        Assert.Equal(202, _locationLinkService.SaveRequests[0].ToLocationId);
        Assert.Equal(SourceType.Manual, _locationService.FacilityViews[12].Links[0].SourceType);
        Assert.Contains("AdjacentRight", cut.Find("[data-testid='outgoing-links-table']").TextContent);
        Assert.DoesNotContain("link-dialog", cut.Markup);
        Assert.Contains("AdjacentRight saved for Kennel 1.", cut.Find("[data-testid='link-save-success']").TextContent);
    }

    [Fact]
    public void Remove_Link_Dialog_Uses_Deactivate_Workflow()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [
                Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1"),
                Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
            ],
            [
                Link(501, 12, 201, "KEN-1", "Kennel 1", LocationType.Kennel, 202, "KEN-2", "Kennel 2", LocationType.Kennel, LinkType.AdjacentRight, SourceType.Manual, null, null),
                Link(502, 12, 202, "KEN-2", "Kennel 2", LocationType.Kennel, 201, "KEN-1", "Kennel 1", LocationType.Kennel, LinkType.AdjacentLeft, SourceType.Manual, null, null)
            ]);
        _locationLinkService.OnRemove = request =>
        {
            _locationService.FacilityViews[12] = View(
                12,
                "PHX",
                "Phoenix Shelter",
                [
                    Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A"),
                    Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1"),
                    Location(202, 12, 101, LocationType.Kennel, "KEN-2", "Kennel 2")
                ]);
            return LocationLinkRemoveResult.Success();
        };

        var cut = Render<AdminLayout>();
        cut.Find("[data-testid='location-item-201']").Click();
        cut.Find("[data-testid='remove-link-button-501']").Click();

        Assert.Contains("deactivates the directed row and its reciprocal row", cut.Find("[data-testid='remove-link-dialog']").TextContent);

        cut.Find("[data-testid='confirm-remove-link-button']").Click();

        Assert.Single(_locationLinkService.RemoveRequests);
        Assert.Equal(201, _locationLinkService.RemoveRequests[0].FromLocationId);
        Assert.Equal(202, _locationLinkService.RemoveRequests[0].ToLocationId);
        Assert.Contains("No active outgoing links", cut.Find("[data-testid='outgoing-links-table']").TextContent);
        Assert.Contains("AdjacentRight was removed.", cut.Find("[data-testid='link-save-success']").TextContent);
    }

    [Fact]
    public void Empty_States_Use_Actionable_Copy()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(
            12,
            "PHX",
            "Phoenix Shelter",
            [Location(101, 12, null, LocationType.Room, "ROOM-A", "Room A")]);
        _facilityMapReadService.RoomMaps[(12, 101)] = RoomMap(
            FacilityMapFacility(12, "PHX", "Phoenix Shelter"),
            FacilityMapLocation(101, 12, "ROOM-A", "Room A", LocationType.Room),
            [],
            []);

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-101']").Click();

        Assert.Contains("Add adjacency between kennels or topology links between room-like spaces", cut.Find("[data-testid='links-empty-state']").TextContent);
        Assert.Contains("No kennel children are available for this room yet.", cut.Find("[data-testid='room-placement-empty']").TextContent);
    }

    private static FacilityAdminListItem Facility(int facilityId, string facilityCode, string name) =>
        new(facilityId, facilityCode, name, "America/Phoenix", true, null, DateTime.UtcNow, DateTime.UtcNow);

    private static LocationAdminFacilityView View(
        int facilityId,
        string facilityCode,
        string facilityName,
        IReadOnlyList<LocationAdminListItem> locations,
        IReadOnlyList<LocationAdminLinkListItem>? links = null) =>
        new(facilityId, facilityCode, facilityName, true, locations, links ?? [], BuildTree(locations, null));

    private static LocationAdminListItem Location(
        int locationId,
        int facilityId,
        int? parentLocationId,
        LocationType locationType,
        string locationCode,
        string name,
        int? gridRow = null,
        int? gridColumn = null,
        int stackLevel = 0,
        int? displayOrder = null,
        bool isActive = true,
        string? notes = null) =>
        new(locationId, facilityId, parentLocationId, locationType, locationCode, name, gridRow, gridColumn, stackLevel, displayOrder, isActive, notes);

    private static LocationAdminLinkListItem Link(
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
        SourceType sourceType,
        string? sourceReference,
        string? notes) =>
        new(
            locationLinkId,
            facilityId,
            fromLocationId,
            fromLocationCode,
            fromLocationName,
            fromLocationType,
            toLocationId,
            toLocationCode,
            toLocationName,
            toLocationType,
            linkType,
            sourceType,
            sourceReference,
            notes);

    private static IReadOnlyList<LocationAdminTreeItem> BuildTree(IReadOnlyList<LocationAdminListItem> locations, int? parentLocationId) =>
        locations
            .Where(location => location.ParentLocationId == parentLocationId)
            .OrderBy(location => location.DisplayOrder ?? int.MaxValue)
            .ThenBy(location => location.Name)
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

    private static FacilityMapFacilityOption FacilityMapFacility(int facilityId, string facilityCode, string name) =>
        new(facilityId, new KennelTrace.Domain.Common.FacilityCode(facilityCode), name, true);

    private static FacilityMapLocationDetail FacilityMapLocation(
        int locationId,
        int facilityId,
        string locationCode,
        string name,
        LocationType locationType,
        int? gridRow = null,
        int? gridColumn = null,
        int stackLevel = 0,
        int? displayOrder = null) =>
        new(
            locationId,
            facilityId,
            101,
            locationType,
            new KennelTrace.Domain.Common.LocationCode(locationCode),
            name,
            true,
            gridRow,
            gridColumn,
            stackLevel,
            displayOrder,
            null,
            0,
            []);

    private static RoomMapResult RoomMap(
        FacilityMapFacilityOption facility,
        FacilityMapLocationDetail room,
        IReadOnlyList<FacilityMapLocationDetail> placedLocations,
        IReadOnlyList<FacilityMapLocationDetail> unplacedLocations) =>
        new(facility, room, placedLocations, unplacedLocations);

    private sealed class FakeFacilityAdminService : IFacilityAdminService
    {
        public IReadOnlyList<FacilityAdminListItem> Facilities { get; set; } = [];

        public Task<IReadOnlyList<FacilityAdminListItem>> ListFacilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Facilities);

        public Task<FacilitySaveResult> SaveAsync(FacilitySaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default) =>
            Task.FromResult(FacilitySaveResult.Forbidden());
    }

    private sealed class FakeLocationAdminService : ILocationAdminService
    {
        public Dictionary<int, LocationAdminFacilityView> FacilityViews { get; } = [];

        public List<int> GetFacilityCalls { get; } = [];

        public List<LocationSaveRequest> SaveRequests { get; } = [];

        public Func<LocationSaveRequest, LocationSaveResult>? OnSave { get; set; }

        public Task<LocationAdminFacilityView?> GetFacilityAsync(int facilityId, CancellationToken cancellationToken = default)
        {
            GetFacilityCalls.Add(facilityId);
            return Task.FromResult(FacilityViews.GetValueOrDefault(facilityId));
        }

        public Task<LocationSaveResult> SaveAsync(LocationSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            return Task.FromResult(OnSave?.Invoke(request) ?? LocationSaveResult.Forbidden());
        }
    }

    private sealed class FakeLocationLinkAdminService : ILocationLinkAdminService
    {
        public List<LocationLinkSaveRequest> SaveRequests { get; } = [];

        public List<LocationLinkRemoveRequest> RemoveRequests { get; } = [];

        public Func<LocationLinkSaveRequest, LocationLinkSaveResult>? OnSave { get; set; }

        public Func<LocationLinkRemoveRequest, LocationLinkRemoveResult>? OnRemove { get; set; }

        public Task<LocationLinkSaveResult> SaveAsync(LocationLinkSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            return Task.FromResult(OnSave?.Invoke(request) ?? LocationLinkSaveResult.Forbidden());
        }

        public Task<LocationLinkRemoveResult> RemoveAsync(LocationLinkRemoveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            RemoveRequests.Add(request);
            return Task.FromResult(OnRemove?.Invoke(request) ?? LocationLinkRemoveResult.Forbidden());
        }
    }

    private sealed class FakeFacilityMapReadService : IFacilityMapReadService
    {
        public Dictionary<(int FacilityId, int RoomLocationId), RoomMapResult?> RoomMaps { get; } = [];

        public Task<IReadOnlyList<FacilityMapFacilityOption>> ListFacilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FacilityMapFacilityOption>>([]);

        public Task<IReadOnlyList<FacilityMapRoomOption>> ListRoomsAsync(int facilityId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FacilityMapRoomOption>>([]);

        public Task<RoomMapResult?> GetRoomMapAsync(int facilityId, int roomLocationId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RoomMaps.GetValueOrDefault((facilityId, roomLocationId)));
    }
}
