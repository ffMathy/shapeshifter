// ail-derived-from: ChangeSettings@0f6b1c8d37e3d5a5
// End-to-end check for ChangeSettings: its scenarios, played on an X11 desktop with a system tray against the running
// app. It changes settings only through the settings window, as a user would, and puts each back afterwards, so the
// other checks find the defaults. It sees only windows on screen, the clipboard, the text that lands in another app
// and what would have opened in the browser (see AppDriver.cs).
using System.Text;
using static Shapeshifter.Checks.AppDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class ChangeSettingsChecks(AppFixture target) : Scenario, IClassFixture<AppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await CloseSettings(); await ReleaseAll(); await WaitForPicker(false); }

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    /// <summary>Holds Ctrl, then V until the picker appears; lets go of V, then of Ctrl, and returns what was pasted.</summary>
    async Task<string> PasteSecondItem(string expected)
    {
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("v");
        await KeyUp("ctrl");
        await WaitForPicker(false);
        return await App.WaitForText(expected);
    }

    [Fact(DisplayName = "a longer delay keeps the history hidden for longer", Timeout = 30_000)]
    public async Task LongerDelay()
    {
        await OpenSettings();
        await TypeNumber(SettingsSpot.Delay, 1500);
        await CloseSettings();
        try
        {
            await App.Copy($"slow {Unique()}");
            await App.Prepare();
            await KeyDown("ctrl");
            await KeyDown("v", 0);
            var early = false;
            for (var until = DateTime.UtcNow.AddMilliseconds(1000); DateTime.UtcNow < until; await Sleep(50)) early |= PickerVisible();
            var late = await WaitForPicker(true, 1500);
            await KeyUp("v");
            await Tap("Escape");
            await KeyUp("ctrl");
            Assert.False(early);
            Assert.True(late);
        }
        finally
        {
            await ReleaseAll();
            await WaitForPicker(false);
            await OpenSettings();
            await TypeNumber(SettingsSpot.Delay, 300);
        }
    }

    [Fact(DisplayName = "turning off copy notifications hides the Copied notification", Timeout = 30_000)]
    public async Task CopyNotificationsOff()
    {
        await App.Copy($"noisy {Unique()}");
        await Sleep(2500);
        await OpenSettings();
        await ClickInSettings(SettingsSpot.CopyNotifications);
        await CloseSettings();
        try
        {
            await App.Send($"copy-text {B64($"quiet {Unique()}")}");
            Assert.False(await NotificationAppears(2000));
        }
        finally
        {
            await OpenSettings();
            await ClickInSettings(SettingsSpot.CopyNotifications);
            await CloseSettings();
        }
        await App.Send($"copy-text {B64($"loud {Unique()}")}");
        Assert.True(await NotificationAppears(2000));
    }

    [Fact(DisplayName = "Items to remember limits how much is kept", Timeout = 30_000)]
    public async Task ItemsToRemember()
    {
        await OpenSettings();
        await TypeNumber(SettingsSpot.ItemsToRemember, 1);
        await CloseSettings();
        var id = Unique();
        try
        {
            await App.Copy($"dropped {id}");
            await App.Copy($"kept {id}");
            var pasted = await PasteSecondItem($"kept {id}");
            Assert.NotEqual($"dropped {id}", pasted);
            Assert.Equal($"kept {id}", pasted);
        }
        finally
        {
            await ReleaseAll();
            await OpenSettings();
            await TypeNumber(SettingsSpot.ItemsToRemember, 50);
        }
    }

    [Fact(DisplayName = "Clear history forgets older copies but keeps the clipboard", Timeout = 30_000)]
    public async Task ClearHistory()
    {
        var id = Unique();
        await App.Copy($"before {id}");
        await App.Copy($"current {id}");
        await OpenSettings();
        await PressSettingsButton(SettingsButton.ClearHistory);
        await CloseSettings();
        var pasted = await PasteSecondItem($"current {id}");
        Assert.NotEqual($"before {id}", pasted);
        Assert.Equal($"current {id}", pasted);
    }

    [Fact(DisplayName = "Copy version puts the version and the system on the clipboard", Timeout = 20_000)]
    public async Task CopyVersion()
    {
        await OpenSettings();
        await ClickInSettings(SettingsSpot.CopyVersion);
        Assert.Matches(@"^Shapeshifter \d+\.\d+\.\d+\S* · .+ · (X64|Arm64|X86|Arm)( · (X11|Wayland))?$", ClipboardText());
    }

    [Fact(DisplayName = "Report a problem opens a new GitHub issue", Timeout = 20_000)]
    public async Task ReportAProblem()
    {
        await OpenSettings();
        var before = Opened().Length;
        await ClickInSettings(SettingsSpot.ReportProblem);
        Assert.StartsWith("https://github.com/ffMathy/Shapeshifter/issues/new?labels=bug&body=", await NextOpened(before), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Project page opens the project on GitHub", Timeout = 20_000)]
    public async Task ProjectPage()
    {
        await OpenSettings();
        var before = Opened().Length;
        await ClickInSettings(SettingsSpot.ProjectPage);
        Assert.Equal("https://github.com/ffMathy/Shapeshifter", await NextOpened(before));
    }

    [Fact(DisplayName = "Check for updates opens the releases page when this copy cannot update itself", Timeout = 20_000)]
    public async Task CheckForUpdates()
    {
        await OpenSettings();
        var before = Opened().Length;
        await PressSettingsButton(SettingsButton.CheckForUpdates);
        Assert.Equal("https://github.com/ffMathy/Shapeshifter/releases", await NextOpened(before));
    }

    [Fact(DisplayName = "Open log folder opens the folder of the logs", Timeout = 20_000)]
    public async Task OpenLogFolder()
    {
        await OpenSettings();
        var before = Opened().Length;
        await PressSettingsButton(SettingsButton.OpenLogFolder);
        Assert.Matches(@"^/.*/logs$", await NextOpened(before));
    }
}
