using KennelTrace.Infrastructure.Persistence;
using KennelTrace.Web.Components;
using KennelTrace.Web.Development;
using KennelTrace.Web.Features.Facilities.Admin;
using KennelTrace.Web.Security;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddKennelTraceSecurity(builder.Configuration);
builder.Services.AddKennelTraceSqlServer(builder.Configuration);
builder.Services.AddScoped<IFacilityAdminService, FacilityAdminService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    await app.Services.ApplyDevelopmentDatabaseSetupAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
