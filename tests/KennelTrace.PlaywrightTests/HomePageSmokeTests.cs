using System.Text.RegularExpressions;
using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace KennelTrace.PlaywrightTests;

public sealed class HomePageSmokeTests
{
    [Fact(Skip = "Requires Playwright browser install and a running app URL in KENNELTRACE_BASE_URL.")]
    public async Task HomePageShowsApplicationName()
    {
        var baseUrl = Environment.GetEnvironmentVariable("KENNELTRACE_BASE_URL")
            ?? throw new InvalidOperationException("Set KENNELTRACE_BASE_URL to the running KennelTrace.Web base URL.");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });

        var page = await browser.NewPageAsync();
        await page.GotoAsync(baseUrl);

        await Expect(page).ToHaveTitleAsync(new Regex("KennelTrace", RegexOptions.IgnoreCase));
        await Expect(page.GetByText("KennelTrace")).ToBeVisibleAsync();
    }
}
