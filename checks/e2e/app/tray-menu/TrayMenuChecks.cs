// ail-derived-from: TrayMenu@8b8636bf5f214da9
// End-to-end check for TrayMenu: its scenarios, played on an X11 desktop with a system tray against the running app.
// It sees only what a user sees: windows on screen, the text that lands in another app, and what would have opened
// in the browser or the file manager (see AppDriver.cs for what the desktop needs).
using System.Text.RegularExpressions;
using static Shapeshifter.Checks.AppDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed partial class TrayMenuChecks(AppFixture target) : Scenario, IClassFixture<AppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await CloseSettings(); await ReleaseAll(); await WaitForPicker(false); }

    [GeneratedRegex(@"^Shapeshifter \d+\.\d+\.\d+\S* · .+ · (X64|Arm64|X86|Arm)( · (X11|Wayland))?$", RegexOptions.Multiline)]
    private static partial Regex EnvironmentLine();

    [Fact(DisplayName = "clicking the tray icon opens the settings", Timeout = 20_000)]
    public async Task ClickingTheIcon()
    {
        await ClickTrayIcon();
        Assert.True(await WaitForSettings(true));
    }

    [Fact(DisplayName = "Settings… opens the settings, and only once", Timeout = 20_000)]
    public async Task SettingsOnlyOnce()
    {
        await ChooseFromTrayMenu("Settings…");
        Assert.True(await WaitForSettings(true));
        await ChooseFromTrayMenu("Settings…");
        await WaitForSettings(true);
        Assert.Single(SettingsWindows());
    }

    [Fact(DisplayName = "About Shapeshifter opens the settings", Timeout = 20_000)]
    public async Task About()
    {
        await ChooseFromTrayMenu("About Shapeshifter");
        Assert.True(await WaitForSettings(true));
    }

    [Fact(DisplayName = "Report a problem… opens a new GitHub issue with the version filled in", Timeout = 20_000)]
    public async Task ReportAProblem()
    {
        var before = Opened().Length;
        await ChooseFromTrayMenu("Report a problem…");
        var url = await NextOpened(before);
        Assert.NotNull(url);
        Assert.StartsWith("https://github.com/ffMathy/Shapeshifter/issues/new?labels=bug&body=", url, StringComparison.Ordinal);
        var body = Uri.UnescapeDataString(url[(url.IndexOf("body=", StringComparison.Ordinal) + 5)..]);
        Assert.StartsWith("**What happened?**", body, StringComparison.Ordinal);
        Assert.Contains("**What did you expect to happen?**", body, StringComparison.Ordinal);
        Assert.Contains("**Steps to reproduce** (which application you copied from and pasted into)", body, StringComparison.Ordinal);
        Assert.Matches(EnvironmentLine(), body);
        Assert.Empty(SettingsWindows());
    }

    [Fact(DisplayName = "Open log folder opens the folder of the logs", Timeout = 20_000)]
    public async Task OpenLogFolder()
    {
        var before = Opened().Length;
        await ChooseFromTrayMenu("Open log folder");
        Assert.Matches(@"^/.*/logs$", await NextOpened(before));
    }

    [Fact(DisplayName = "Check for updates… opens the releases page when this copy cannot update itself", Timeout = 20_000)]
    public async Task CheckForUpdates()
    {
        var before = Opened().Length;
        await ChooseFromTrayMenu("Check for updates…");
        Assert.Equal("https://github.com/ffMathy/Shapeshifter/releases", await NextOpened(before));
        Assert.Empty(SettingsWindows());
    }

    [Fact(DisplayName = "Clear history forgets what was copied before", Timeout = 30_000)]
    public async Task ClearHistory()
    {
        var id = Unique();
        await App.Copy($"older {id}");
        await App.Copy($"latest {id}");
        await ChooseFromTrayMenu("Clear history");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("v");
        await KeyUp("ctrl");
        await WaitForPicker(false);
        var pasted = await App.WaitForText($"latest {id}");
        Assert.NotEqual($"older {id}", pasted);
        Assert.Equal($"latest {id}", pasted);
    }
}
