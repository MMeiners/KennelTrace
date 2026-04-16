using System.Security.Claims;
using Bunit;
using KennelTrace.Domain.Features.Locations;
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
    private readonly FakeLocationAdminService _locationService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminLayoutPageTests()
    {
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IFacilityAdminService>(_facilityService);
        Services.AddSingleton<ILocationAdminService>(_locationService);

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
                Location(201, 12, 101, LocationType.Kennel, "KEN-1", "Kennel 1", notes: "Window side")
            ]);

        var cut = Render<AdminLayout>();

        cut.Find("[data-testid='location-item-201']").Click();

        Assert.Equal("KEN-1", cut.Find("[data-testid='location-code-input']").GetAttribute("value"));
        Assert.Equal("Kennel 1", cut.Find("[data-testid='location-name-input']").GetAttribute("value"));
        Assert.Contains("Edit Location", cut.Find("[data-testid='location-form']").TextContent);
    }

    [Fact]
    public void Create_Flow_Saves_And_Refreshes_The_Browser()
    {
        _facilityService.Facilities = [Facility(12, "PHX", "Phoenix Shelter")];
        _locationService.FacilityViews[12] = View(12, "PHX", "Phoenix Shelter", []);
        _locationService.OnSave = request =>
        {
            var createdLocation = Location(301, 12, null, request.LocationType, request.LocationCode, request.Name, request.DisplayOrder, request.IsActive, request.Notes);
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
            var updatedLocation = Location(101, 12, null, request.LocationType, request.LocationCode, request.Name, request.DisplayOrder, request.IsActive, request.Notes);
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

    private static FacilityAdminListItem Facility(int facilityId, string facilityCode, string name) =>
        new(facilityId, facilityCode, name, "America/Phoenix", true, null, DateTime.UtcNow, DateTime.UtcNow);

    private static LocationAdminFacilityView View(int facilityId, string facilityCode, string facilityName, IReadOnlyList<LocationAdminListItem> locations) =>
        new(facilityId, facilityCode, facilityName, true, locations, BuildTree(locations, null));

    private static LocationAdminListItem Location(
        int locationId,
        int facilityId,
        int? parentLocationId,
        LocationType locationType,
        string locationCode,
        string name,
        int? displayOrder = null,
        bool isActive = true,
        string? notes = null) =>
        new(locationId, facilityId, parentLocationId, locationType, locationCode, name, displayOrder, isActive, notes);

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
}
