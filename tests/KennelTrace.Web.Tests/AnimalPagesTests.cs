using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Domain.Features.Locations;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class AnimalPagesTests : BunitContext
{
    private readonly FakeAnimalReadService _service = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AnimalPagesTests()
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
        Services.AddSingleton<IAnimalReadService>(_service);
    }

    [Fact]
    public void Animals_Page_Initial_Render_Shows_Search_And_Honest_Empty_State()
    {
        SetReadOnlyUser();

        var cut = Render<Animals>();

        Assert.Contains("Animals", cut.Markup);
        Assert.NotNull(cut.Find("#animal-search-input"));
        Assert.Contains("Enter an animal number or name to search.", cut.Find("[data-testid='animals-initial-state']").TextContent);
        Assert.Empty(_service.LookupCalls);
    }

    [Fact]
    public void Animals_Page_Search_Renders_Results()
    {
        SetReadOnlyUser();
        _service.LookupResults["A-10"] =
        [
            new AnimalLookupRow(10, new AnimalCode("A-10"), "Milo", "Dog", true),
            new AnimalLookupRow(11, new AnimalCode("A-101"), null, "Dog", false)
        ];

        var cut = Render<Animals>();

        cut.Find("#animal-search-input").Input("A-10");
        cut.Find("button").Click();

        Assert.Equal(["A-10"], _service.LookupCalls);
        var table = cut.Find("[data-testid='animals-results-table']");
        Assert.Contains("Milo", table.TextContent);
        Assert.Contains("Unnamed animal", table.TextContent);
        Assert.Contains("/animals/10", table.InnerHtml);
        Assert.Contains("/animals/11", table.InnerHtml);
    }

    [Fact]
    public void Animals_Page_Shows_Empty_Search_Result_State()
    {
        SetReadOnlyUser();
        _service.LookupResults["missing"] = [];

        var cut = Render<Animals>();

        cut.Find("#animal-search-input").Input("missing");
        cut.Find("button").Click();

        Assert.Equal(["missing"], _service.LookupCalls);
        Assert.Contains("No animals matched that search.", cut.Find("[data-testid='animals-empty-state']").TextContent);
    }

    [Fact]
    public void Animal_Detail_Renders_Summary_And_History()
    {
        SetReadOnlyUser();
        _service.DetailsByAnimalId[42] = DetailWithCurrentPlacement();

        var cut = Render<AnimalDetail>(parameters => parameters.Add(x => x.AnimalId, 42));

        Assert.Equal([42], _service.DetailCalls);
        Assert.Contains("A-42", cut.Find("[data-testid='animal-detail-number']").TextContent);
        Assert.Contains("Luna", cut.Markup);
        Assert.Contains("Dog", cut.Markup);
        Assert.Contains("Shepherd", cut.Markup);
        Assert.Contains("Movement history", cut.Markup);
    }

    [Fact]
    public void Animal_Detail_Shows_Current_Placement_Clearly()
    {
        SetReadOnlyUser();
        _service.DetailsByAnimalId[42] = DetailWithCurrentPlacement();

        var cut = Render<AnimalDetail>(parameters => parameters.Add(x => x.AnimalId, 42));

        Assert.Contains("Kennel 7 (KEN-7)", cut.Find("[data-testid='animal-current-location']").TextContent);
        Assert.Contains("Phoenix Main (PHX)", cut.Find("[data-testid='animal-current-facility']").TextContent);
        Assert.Contains("Ward A (ROOM-A)", cut.Markup);
    }

    [Fact]
    public void Animal_Detail_Labels_Open_Stay_As_Current()
    {
        SetReadOnlyUser();
        _service.DetailsByAnimalId[42] = DetailWithCurrentPlacement();

        var cut = Render<AnimalDetail>(parameters => parameters.Add(x => x.AnimalId, 42));

        Assert.Contains("Current", cut.Find("[data-testid='animal-current-chip']").TextContent);
        Assert.Contains("Current", cut.Find("[data-testid='movement-current-7001']").TextContent);
        Assert.Contains("Open stay", cut.Markup);
    }

    [Fact]
    public void Animal_Detail_Renders_Movement_History_Table()
    {
        SetReadOnlyUser();
        _service.DetailsByAnimalId[42] = DetailWithCurrentPlacement();

        var cut = Render<AnimalDetail>(parameters => parameters.Add(x => x.AnimalId, 42));

        var historyTable = cut.Find("[data-testid='animal-history-table']");
        Assert.Contains("Phoenix Main (PHX)", historyTable.TextContent);
        Assert.Contains("Isolation Intake (ISO-1)", historyTable.TextContent);
        Assert.Contains("Medical review", historyTable.TextContent);
    }

    [Fact]
    public void Animals_Route_Renders_For_ReadOnly_User()
    {
        SetReadOnlyUser();

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(Animals), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("Animals", cut.Markup);
    }

    [Fact]
    public void Animals_Route_Is_Rejected_For_Anonymous_User()
    {
        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<AuthorizeRouteView>(0);
                childBuilder.AddAttribute(1, "RouteData", new RouteData(typeof(Animals), new Dictionary<string, object?>()));
                childBuilder.AddAttribute(2, "DefaultLayout", typeof(TestLayout));
                childBuilder.AddAttribute(3, "NotAuthorized", (RenderFragment<AuthenticationState>)(_ => notAuthorizedBuilder =>
                {
                    notAuthorizedBuilder.AddContent(0, "blocked");
                }));
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("blocked", cut.Markup);
        Assert.DoesNotContain("Enter an animal number or name to search.", cut.Markup);
    }

    private void SetReadOnlyUser() =>
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

    private static AnimalDetailResult DetailWithCurrentPlacement() =>
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
                new DateTime(2026, 4, 16, 17, 45, 0, DateTimeKind.Utc),
                3,
                new FacilityCode("PHX"),
                "Phoenix Main",
                77,
                new LocationCode("KEN-7"),
                "Kennel 7",
                LocationType.Kennel,
                true,
                12,
                new LocationCode("ROOM-A"),
                "Ward A",
                LocationType.Room),
            [
                new AnimalMovementHistoryRow(
                    7001,
                    new DateTime(2026, 4, 16, 17, 45, 0, DateTimeKind.Utc),
                    null,
                    "Transfer from intake",
                    3,
                    new FacilityCode("PHX"),
                    "Phoenix Main",
                    77,
                    new LocationCode("KEN-7"),
                    "Kennel 7",
                    LocationType.Kennel,
                    true,
                    12,
                    new LocationCode("ROOM-A"),
                    "Ward A",
                    LocationType.Room),
                new AnimalMovementHistoryRow(
                    7000,
                    new DateTime(2026, 4, 16, 13, 15, 0, DateTimeKind.Utc),
                    new DateTime(2026, 4, 16, 17, 45, 0, DateTimeKind.Utc),
                    "Medical review",
                    3,
                    new FacilityCode("PHX"),
                    "Phoenix Main",
                    55,
                    new LocationCode("ISO-1"),
                    "Isolation Intake",
                    LocationType.Isolation,
                    true,
                    55,
                    new LocationCode("ISO-1"),
                    "Isolation Intake",
                    LocationType.Isolation)
            ]);

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public Dictionary<string, IReadOnlyList<AnimalLookupRow>> LookupResults { get; } = [];

        public Dictionary<int, AnimalDetailResult?> DetailsByAnimalId { get; } = [];

        public List<string?> LookupCalls { get; } = [];

        public List<int> DetailCalls { get; } = [];

        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default)
        {
            LookupCalls.Add(searchText);
            return Task.FromResult(LookupResults.GetValueOrDefault(searchText ?? string.Empty, Array.Empty<AnimalLookupRow>()));
        }

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default)
        {
            DetailCalls.Add(animalId);
            return Task.FromResult(DetailsByAnimalId.GetValueOrDefault(animalId));
        }
    }

    private sealed class TestLayout : LayoutComponentBase
    {
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, Body);
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
