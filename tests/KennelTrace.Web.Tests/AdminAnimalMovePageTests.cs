using System.Security.Claims;
using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Animals.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class AdminAnimalMovePageTests : BunitContext
{
    private readonly FakeAnimalReadService _readService = new();
    private readonly FakeAnimalMovementAdminService _movementAdminService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminAnimalMovePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.RemoveAll<IKeyInterceptorService>();
        Services.AddSingleton<IKeyInterceptorService, TestKeyInterceptorService>();
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IAnimalReadService>(_readService);
        Services.AddSingleton<IAnimalMovementAdminService>(_movementAdminService);

        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);
        _readService.MoveLocations =
        [
            new AnimalMoveLocationOption(100, 1, new FacilityCode("PHX"), "Phoenix Main", new LocationCode("KEN-1"), "Kennel 1", LocationType.Kennel, true),
            new AnimalMoveLocationOption(200, 2, new FacilityCode("TUC"), "Tucson Annex", new LocationCode("ISO-1"), "Isolation 1", LocationType.Isolation, false)
        ];
    }

    [Fact]
    public void Move_Entry_Form_Renders_With_Animal_Summary_And_Current_Placement()
    {
        _readService.DetailsByAnimalId[42] = DetailWithCurrentPlacement("KEN-1", "Kennel 1", "Transfer from intake");

        var cut = Render<AdminAnimalMove>(parameters => parameters.Add(x => x.AnimalId, 42));

        Assert.Contains("Record Move", cut.Markup);
        Assert.Contains("A-42", cut.Find("[data-testid='animal-move-animal-summary']").TextContent);
        Assert.Contains("Kennel 1 (KEN-1)", cut.Find("[data-testid='animal-move-current-placement']").TextContent);
        Assert.NotNull(cut.Find("[data-testid='animal-move-location-input']"));
        Assert.NotNull(cut.Find("[data-testid='animal-move-start-input']"));
        Assert.NotNull(cut.Find("[data-testid='animal-move-end-input']"));
        Assert.NotNull(cut.Find("[data-testid='animal-move-reason-input']"));
        Assert.NotNull(cut.Find("[data-testid='animal-move-notes-input']"));
    }

    [Fact]
    public void Failed_Save_Displays_Validation_Errors()
    {
        _readService.DetailsByAnimalId[42] = DetailWithCurrentPlacement("KEN-1", "Kennel 1", "Transfer from intake");
        _movementAdminService.OnSave = _ => RecordAnimalStayResult.ValidationFailed(new Dictionary<string, string[]>
        {
            [nameof(RecordAnimalStayRequest.StartUtc)] = ["The requested stay overlaps existing movement history for this animal."]
        });

        var cut = Render<AdminAnimalMove>(parameters => parameters.Add(x => x.AnimalId, 42));

        cut.Find("[data-testid='animal-move-location-input']").Change("100");
        cut.Find("[data-testid='animal-move-start-input']").Input("2026-04-17T12:30");
        cut.Find("[data-testid='animal-move-save-button']").Click();

        Assert.Single(_movementAdminService.SaveRequests);
        Assert.Contains("overlaps existing movement history", cut.Markup);
    }

    [Fact]
    public void Successful_Save_Flow_Navigates_Back_To_Detail_Page()
    {
        _readService.DetailsByAnimalId[42] = DetailWithCurrentPlacement("KEN-1", "Kennel 1", "Transfer from intake");
        _movementAdminService.OnSave = request => RecordAnimalStayResult.Success(new AnimalMovementAdminRecord(
            9001,
            request.AnimalId,
            request.LocationId,
            request.StartUtc,
            request.EndUtc,
            request.MovementReason,
            "admin-user",
            request.Notes,
            DateTime.UtcNow,
            DateTime.UtcNow));

        var cut = Render<AdminAnimalMove>(parameters => parameters.Add(x => x.AnimalId, 42));

        cut.Find("[data-testid='animal-move-location-input']").Change("100");
        cut.Find("[data-testid='animal-move-start-input']").Input("2026-04-17T12:30");
        cut.Find("[data-testid='animal-move-reason-input']").Input("Isolation intake");
        cut.Find("[data-testid='animal-move-save-button']").Click();

        Assert.Single(_movementAdminService.SaveRequests);
        Assert.Equal(42, _movementAdminService.SaveRequests[0].AnimalId);
        Assert.Equal(100, _movementAdminService.SaveRequests[0].LocationId);
        Assert.Equal("http://localhost/animals/42", Services.GetRequiredService<NavigationManager>().Uri);
    }

    [Fact]
    public void Detail_Page_Shows_Updated_Current_Placement_After_Save()
    {
        _readService.DetailsByAnimalId[42] = DetailWithCurrentPlacement("KEN-1", "Kennel 1", "Transfer from intake");
        _movementAdminService.OnSave = request =>
        {
            _readService.DetailsByAnimalId[42] = DetailWithCurrentPlacement("ISO-1", "Isolation 1", request.MovementReason ?? "Isolation intake");
            return RecordAnimalStayResult.Success(new AnimalMovementAdminRecord(
                9002,
                request.AnimalId,
                request.LocationId,
                request.StartUtc,
                request.EndUtc,
                request.MovementReason,
                "admin-user",
                request.Notes,
                DateTime.UtcNow,
                DateTime.UtcNow));
        };

        var movePage = Render<AdminAnimalMove>(parameters => parameters.Add(x => x.AnimalId, 42));

        movePage.Find("[data-testid='animal-move-location-input']").Change("200");
        movePage.Find("[data-testid='animal-move-start-input']").Input("2026-04-17T12:30");
        movePage.Find("[data-testid='animal-move-reason-input']").Input("Isolation intake");
        movePage.Find("[data-testid='animal-move-save-button']").Click();

        var detailPage = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AnimalDetail>(0);
                childBuilder.AddAttribute(1, nameof(AnimalDetail.AnimalId), 42);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Isolation 1 (ISO-1)", detailPage.Find("[data-testid='animal-current-location']").TextContent);
        Assert.Contains("Current", detailPage.Find("[data-testid='animal-current-chip']").TextContent);
        Assert.Contains("Isolation intake", detailPage.Find("[data-testid='animal-history-table']").TextContent);
        Assert.Contains("Current", detailPage.Markup);
    }

    private static AnimalDetailResult DetailWithCurrentPlacement(string locationCode, string locationName, string movementReason) =>
        new(
            42,
            new AnimalCode("A-42"),
            "Luna",
            "Dog",
            "Female",
            "Shepherd",
            new DateOnly(2022, 5, 4),
            true,
            null,
            new AnimalCurrentPlacementSummary(
                7001,
                new DateTime(2026, 4, 17, 11, 15, 0, DateTimeKind.Utc),
                1,
                new FacilityCode("PHX"),
                "Phoenix Main",
                locationCode == "ISO-1" ? 200 : 100,
                new LocationCode(locationCode),
                locationName,
                locationCode == "ISO-1" ? LocationType.Isolation : LocationType.Kennel,
                true,
                12,
                new LocationCode("ROOM-A"),
                "Ward A",
                LocationType.Room),
            [
                new AnimalMovementHistoryRow(
                    7001,
                    new DateTime(2026, 4, 17, 11, 15, 0, DateTimeKind.Utc),
                    null,
                    movementReason,
                    1,
                    new FacilityCode("PHX"),
                    "Phoenix Main",
                    locationCode == "ISO-1" ? 200 : 100,
                    new LocationCode(locationCode),
                    locationName,
                    locationCode == "ISO-1" ? LocationType.Isolation : LocationType.Kennel,
                    true,
                    12,
                    new LocationCode("ROOM-A"),
                    "Ward A",
                    LocationType.Room),
                new AnimalMovementHistoryRow(
                    7000,
                    new DateTime(2026, 4, 17, 8, 00, 0, DateTimeKind.Utc),
                    new DateTime(2026, 4, 17, 11, 15, 0, DateTimeKind.Utc),
                    "Initial placement",
                    1,
                    new FacilityCode("PHX"),
                    "Phoenix Main",
                    101,
                    new LocationCode("KEN-0"),
                    "Kennel 0",
                    LocationType.Kennel,
                    true,
                    12,
                    new LocationCode("ROOM-A"),
                    "Ward A",
                    LocationType.Room)
            ]);

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public Dictionary<int, AnimalDetailResult?> DetailsByAnimalId { get; } = [];

        public IReadOnlyList<AnimalMoveLocationOption> MoveLocations { get; set; } = [];

        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalLookupRow>>([]);

        public Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(MoveLocations);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DetailsByAnimalId.GetValueOrDefault(animalId));
    }

    private sealed class FakeAnimalMovementAdminService : IAnimalMovementAdminService
    {
        public List<RecordAnimalStayRequest> SaveRequests { get; } = [];

        public Func<RecordAnimalStayRequest, RecordAnimalStayResult>? OnSave { get; set; }

        public Task<RecordAnimalStayResult> RecordStayAsync(RecordAnimalStayRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            return Task.FromResult(OnSave?.Invoke(request) ?? RecordAnimalStayResult.Forbidden());
        }
    }

    private sealed class TestKeyInterceptorService : IKeyInterceptorService, IDisposable
    {
        public Task SubscribeAsync(IKeyInterceptorObserver observer, KeyInterceptorOptions options) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, IKeyDownObserver? keyDown = null, IKeyUpObserver? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyboardEventArgs>? keyDown = null, Action<KeyboardEventArgs>? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Func<KeyboardEventArgs, Task>? keyDown = null, Func<KeyboardEventArgs, Task>? keyUp = null) => Task.CompletedTask;

        public Task SubscribeAsync(string elementId, KeyInterceptorOptions options, Action<KeyMapBuilder> configure) => Task.CompletedTask;

        public Task UpdateKeyAsync(IKeyInterceptorObserver observer, KeyOptions option) => Task.CompletedTask;

        public Task UpdateKeyAsync(string elementId, KeyOptions option) => Task.CompletedTask;

        public Task UnsubscribeAsync(IKeyInterceptorObserver observer) => Task.CompletedTask;

        public Task UnsubscribeAsync(string elementId) => Task.CompletedTask;

        public Task DispatchAsync(string elementId, KeyEventKind keyEventKind, KeyboardEventArgs args) => Task.CompletedTask;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
