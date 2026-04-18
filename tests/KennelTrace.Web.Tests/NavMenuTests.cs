using Bunit;
using KennelTrace.Web.Components.Layout;
using KennelTrace.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class NavMenuTests : BunitContext
{
    private readonly TestAuthenticationStateProvider _authenticationStateProvider = new();

    public NavMenuTests()
    {
        Services.AddMudServices();
        Services.RemoveAll<IAuthorizationService>();
        Services.RemoveAll<IAuthorizationPolicyProvider>();
        Services.RemoveAll<IAuthorizationHandlerProvider>();
        Services.RemoveAll<IAuthorizationHandlerContextFactory>();
        Services.RemoveAll<IAuthorizationEvaluator>();
        Services.AddAuthorizationCore();
        Services.AddSingleton<AuthenticationStateProvider>(_authenticationStateProvider);
    }

    [Fact]
    public void Admin_User_Sees_Admin_Layout_Link()
    {
        _authenticationStateProvider.SetUser("admin-user", KennelTraceRoles.Admin, KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Contains("KennelTrace", cut.Markup);

        var links = cut.FindAll("a.nav-link")
            .Select(link => link.GetAttribute("href"))
            .ToArray();

        Assert.Contains(string.Empty, links);
        Assert.Contains("facility-map", links);
        Assert.Contains("animals", links);
        Assert.Contains("trace", links);
        Assert.Contains("admin/layout", links);
        Assert.Contains("admin/animals", links);
        Assert.Contains("admin/imports", links);
        Assert.DoesNotContain("counter", links);
        Assert.DoesNotContain("weather", links);
        Assert.DoesNotContain("mudblazor-test", links);
    }

    [Fact]
    public void ReadOnly_User_Does_Not_See_Admin_Layout_Link()
    {
        _authenticationStateProvider.SetUser("readonly-user", KennelTraceRoles.ReadOnly);

        var cut = Render(builder =>
        {
            builder.OpenComponent<CascadingAuthenticationState>(0);
            builder.AddAttribute(1, "ChildContent", (RenderFragment)(childBuilder =>
            {
                childBuilder.OpenComponent<NavMenu>(0);
                childBuilder.CloseComponent();
            }));
            builder.CloseComponent();
        });

        var links = cut.FindAll("a.nav-link")
            .Select(link => link.GetAttribute("href"))
            .ToArray();

        Assert.DoesNotContain("admin/layout", links);
        Assert.DoesNotContain("admin/animals", links);
        Assert.DoesNotContain("admin/imports", links);
        Assert.DoesNotContain("counter", links);
        Assert.DoesNotContain("weather", links);
        Assert.DoesNotContain("mudblazor-test", links);
        Assert.Contains("facility-map", links);
        Assert.Contains("animals", links);
        Assert.Contains("trace", links);
    }
}
