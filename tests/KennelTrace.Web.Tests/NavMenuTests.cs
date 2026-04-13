using Bunit;
using KennelTrace.Web.Components.Layout;
using MudBlazor.Services;

namespace KennelTrace.Web.Tests;

public sealed class NavMenuTests : BunitContext
{
    public NavMenuTests()
    {
        Services.AddMudServices();
    }

    [Fact]
    public void NavMenuRendersExpectedPrimaryLinks()
    {
        var cut = Render<NavMenu>();

        Assert.Contains("KennelTrace", cut.Markup);

        var links = cut.FindAll("a.nav-link")
            .Select(link => link.GetAttribute("href"))
            .ToArray();

        Assert.Contains(string.Empty, links);
        Assert.Contains("facility-map", links);
        Assert.Contains("counter", links);
        Assert.Contains("weather", links);
        Assert.Contains("mudblazor-test", links);
    }
}
