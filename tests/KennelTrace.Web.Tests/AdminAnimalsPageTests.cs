using System.Security.Claims;
using Bunit;
using KennelTrace.Domain.Common;
using KennelTrace.Infrastructure.Features.Animals.AnimalRecords;
using KennelTrace.Web.Components.Pages;
using KennelTrace.Web.Features.Animals.Admin;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace KennelTrace.Web.Tests;

public sealed class AdminAnimalsPageTests : BunitContext
{
    private readonly FakeAnimalReadService _readService = new();
    private readonly FakeAnimalAdminService _adminService = new();
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public AdminAnimalsPageTests()
    {
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
        Services.AddSingleton<IAnimalReadService>(_readService);
        Services.AddSingleton<IAnimalAdminService>(_adminService);

        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);
    }

    [Fact]
    public void Admin_Animals_Page_Renders()
    {
        var cut = Render<AdminAnimals>();

        Assert.Contains("Animal Admin", cut.Markup);
        Assert.NotNull(cut.Find("[data-testid='animal-search-input']"));
        Assert.Contains("Create Animal", cut.Markup);
    }

    [Fact]
    public void Select_Flow_Loads_Animal_Into_Form()
    {
        _readService.LookupResults["A-10"] =
        [
            new AnimalLookupRow(10, new AnimalCode("A-10"), "Milo", "Dog", true)
        ];
        _readService.DetailResults[10] = Detail(10, "A-10", "Milo", "Dog", "Male", "Mix", new DateOnly(2023, 1, 2), true, "Friendly");

        var cut = Render<AdminAnimals>();

        cut.Find("[data-testid='animal-search-input']").Input("A-10");
        cut.Find("[data-testid='animal-search-button']").Click();
        cut.Find("[data-testid='animal-list-item-10']").Click();

        Assert.Equal([10], _readService.DetailCalls);
        Assert.Equal("A-10", cut.Find("[data-testid='animal-number-input']").GetAttribute("value"));
        Assert.Equal("Milo", cut.Find("[data-testid='animal-name-input']").GetAttribute("value"));
        Assert.Equal("Male", cut.Find("[data-testid='animal-sex-input']").GetAttribute("value"));
        Assert.Contains("Edit Animal", cut.Markup);
    }

    [Fact]
    public void Create_Flow_Saves_And_Refreshes_Search_List()
    {
        _adminService.OnSave = request =>
        {
            var savedAnimal = SavedAnimal(31, request.AnimalNumber, request.Name, request.Species, request.Sex, request.Breed, request.DateOfBirth, request.IsActive, request.Notes);
            _readService.LookupResults[request.AnimalNumber] =
            [
                new AnimalLookupRow(savedAnimal.AnimalId, new AnimalCode(savedAnimal.AnimalNumber), savedAnimal.Name, savedAnimal.Species, savedAnimal.IsActive)
            ];
            _readService.DetailResults[savedAnimal.AnimalId] = Detail(
                savedAnimal.AnimalId,
                savedAnimal.AnimalNumber,
                savedAnimal.Name,
                savedAnimal.Species,
                savedAnimal.Sex,
                savedAnimal.Breed,
                savedAnimal.DateOfBirth,
                savedAnimal.IsActive,
                savedAnimal.Notes);
            return AnimalSaveResult.Success(savedAnimal);
        };

        var cut = Render<AdminAnimals>();

        cut.Find("[data-testid='animal-number-input']").Input("A-31");
        cut.Find("[data-testid='animal-name-input']").Input("Nova");
        cut.Find("[data-testid='animal-breed-input']").Input("Terrier");
        cut.Find("[data-testid='animal-save-button']").Click();

        Assert.Single(_adminService.SaveRequests);
        Assert.Equal("A-31", _adminService.SaveRequests[0].AnimalNumber);
        Assert.Equal(["A-31"], _readService.LookupCalls);
        Assert.Contains("A-31 saved.", cut.Find("[data-testid='animal-save-success']").TextContent);
        Assert.Contains("Nova", cut.Find("[data-testid='animal-list']").TextContent);
    }

    [Fact]
    public void Edit_Flow_Saves_Selected_Animal()
    {
        _readService.LookupResults["A-10"] =
        [
            new AnimalLookupRow(10, new AnimalCode("A-10"), "Milo", "Dog", true)
        ];
        _readService.DetailResults[10] = Detail(10, "A-10", "Milo", "Dog", "Male", "Mix", null, true, null);
        _adminService.OnSave = request =>
        {
            var savedAnimal = SavedAnimal(request.AnimalId ?? 10, request.AnimalNumber, request.Name, request.Species, request.Sex, request.Breed, request.DateOfBirth, request.IsActive, request.Notes);
            _readService.LookupResults[request.AnimalNumber] =
            [
                new AnimalLookupRow(savedAnimal.AnimalId, new AnimalCode(savedAnimal.AnimalNumber), savedAnimal.Name, savedAnimal.Species, savedAnimal.IsActive)
            ];
            _readService.DetailResults[savedAnimal.AnimalId] = Detail(
                savedAnimal.AnimalId,
                savedAnimal.AnimalNumber,
                savedAnimal.Name,
                savedAnimal.Species,
                savedAnimal.Sex,
                savedAnimal.Breed,
                savedAnimal.DateOfBirth,
                savedAnimal.IsActive,
                savedAnimal.Notes);
            return AnimalSaveResult.Success(savedAnimal);
        };

        var cut = Render<AdminAnimals>();

        cut.Find("[data-testid='animal-search-input']").Input("A-10");
        cut.Find("[data-testid='animal-search-button']").Click();
        cut.Find("[data-testid='animal-list-item-10']").Click();
        cut.Find("[data-testid='animal-name-input']").Input("Milo Updated");
        cut.Find("[data-testid='animal-active-input']").Change(false);
        cut.Find("[data-testid='animal-save-button']").Click();

        Assert.Equal(10, _adminService.SaveRequests.Single().AnimalId);
        Assert.Equal("Milo Updated", _adminService.SaveRequests.Single().Name);
        Assert.False(_adminService.SaveRequests.Single().IsActive);
        Assert.Equal("Milo Updated", cut.Find("[data-testid='animal-name-input']").GetAttribute("value"));
    }

    [Fact]
    public void Validation_Message_Is_Displayed()
    {
        _adminService.OnSave = _ => AnimalSaveResult.ValidationFailed(new Dictionary<string, string[]>
        {
            [nameof(AnimalSaveRequest.AnimalNumber)] = ["Animal number must be unique."]
        });

        var cut = Render<AdminAnimals>();

        cut.Find("[data-testid='animal-number-input']").Input("A-10");
        cut.Find("[data-testid='animal-save-button']").Click();

        Assert.Contains("Animal number must be unique.", cut.Markup);
    }

    private static AnimalDetailResult Detail(
        int animalId,
        string animalNumber,
        string? name,
        string species,
        string? sex,
        string? breed,
        DateOnly? dateOfBirth,
        bool isActive,
        string? notes) =>
        new(animalId, new AnimalCode(animalNumber), name, species, sex, breed, dateOfBirth, isActive, notes, null, []);

    private static AnimalAdminRecord SavedAnimal(
        int animalId,
        string animalNumber,
        string? name,
        string species,
        string? sex,
        string? breed,
        DateOnly? dateOfBirth,
        bool isActive,
        string? notes) =>
        new(animalId, animalNumber, name, species, sex, breed, dateOfBirth, isActive, notes, DateTime.UtcNow, DateTime.UtcNow);

    private sealed class FakeAnimalReadService : IAnimalReadService
    {
        public Dictionary<string, IReadOnlyList<AnimalLookupRow>> LookupResults { get; } = [];

        public Dictionary<int, AnimalDetailResult?> DetailResults { get; } = [];

        public List<string?> LookupCalls { get; } = [];

        public List<int> DetailCalls { get; } = [];

        public Task<IReadOnlyList<AnimalLookupRow>> LookupAnimalsAsync(string? searchText, CancellationToken cancellationToken = default)
        {
            LookupCalls.Add(searchText);
            return Task.FromResult(LookupResults.GetValueOrDefault(searchText ?? string.Empty, Array.Empty<AnimalLookupRow>()));
        }

        public Task<IReadOnlyList<AnimalMoveLocationOption>> ListMoveLocationsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AnimalMoveLocationOption>>([]);

        public Task<AnimalDetailResult?> GetAnimalDetailAsync(int animalId, CancellationToken cancellationToken = default)
        {
            DetailCalls.Add(animalId);
            return Task.FromResult(DetailResults.GetValueOrDefault(animalId));
        }
    }

    private sealed class FakeAnimalAdminService : IAnimalAdminService
    {
        public List<AnimalSaveRequest> SaveRequests { get; } = [];

        public Func<AnimalSaveRequest, AnimalSaveResult>? OnSave { get; set; }

        public Task<AnimalSaveResult> SaveAsync(AnimalSaveRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            SaveRequests.Add(request);
            return Task.FromResult(OnSave?.Invoke(request) ?? AnimalSaveResult.Forbidden());
        }
    }
}
