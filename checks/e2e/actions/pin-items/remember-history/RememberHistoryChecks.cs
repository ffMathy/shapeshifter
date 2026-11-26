// ail-derived-from: RememberHistory@24ab9c40f887e1a6
// End-to-end check for RememberHistory: the "Remember history after restart" setting, switched in the settings
// window (opened from the tray icon), and what comes back after quitting Shapeshifter from its tray menu and
// starting it again. Each scenario runs on a desktop of its own (PrivateDesktop); the saved history is a folder the
// user can see in Shapeshifter's data folder.
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class RememberHistoryChecks : PrivateDesktopScenario
{
    [Fact(DisplayName = "with the setting on, the whole history comes back in its order", Timeout = 300_000)]
    public async Task SettingOnBringsItAllBack()
    {
        var id = ReadableId();
        var first = await SetUp();
        await Desk!.ToggleRememberHistory();
        foreach (var t in new[] { "apple", "banana", "cherry" }) await first.Copy($"{t} {id}");
        await Sleep(1000);
        await Desk.QuitApp();
        // The app that copied goes too, so nothing is on the clipboard when Shapeshifter starts again.
        await first.DisposeAsync();
        App = null;
        await Desk.StartApp();
        var fresh = await TargetApp.Start();
        App = fresh;
        await fresh.Prepare();
        await OpenPicker();
        var history = ReadPicker("history", $"cherry {id}", $"banana {id}", $"apple {id}");
        int At(string t) => history.IndexOf($"{t} {id}", StringComparison.Ordinal);
        Assert.True(At("cherry") >= 0, history);
        Assert.True(At("cherry") < At("banana"), history);
        Assert.True(At("banana") < At("apple"), history);
        await Tap("3");
        await KeyUp("ctrl");
        Assert.Equal($"apple {id}", await fresh.WaitForText($"apple {id}"));
    }

    [Fact(DisplayName = "with the setting off, only pinned items come back", Timeout = 300_000)]
    public async Task SettingOffBringsOnlyPins()
    {
        var id = ReadableId();
        var app = await SetUp();
        await app.Copy($"lost {id}");
        await app.Copy($"current {id}");
        await Desk!.QuitApp();
        await Desk.StartApp();
        await app.Prepare();
        await OpenPicker();
        Assert.Contains($"current {id}", ReadPicker("history", $"current {id}"));
        Assert.DoesNotContain($"lost {id}", ReadPicker("history"));
        await Tap("Escape");
        await KeyUp("ctrl");
        Assert.False(Directory.Exists(Path.Combine(Desk.Home, "data", "History")) || File.Exists(Path.Combine(Desk.Home, "data", "History")));
    }

    [Fact(DisplayName = "turning the setting off deletes the saved history", Timeout = 300_000)]
    public async Task TurningItOffDeletes()
    {
        var id = ReadableId();
        var app = await SetUp();
        await Desk!.ToggleRememberHistory();
        await app.Copy($"saved {id}");
        await Sleep(1000);
        var saved = Path.Combine(Desk.Home, "data", "History");
        Assert.True(File.Exists(Path.Combine(saved, "order.json")));
        await Desk.ToggleRememberHistory();
        await Sleep(1000);
        Assert.False(Directory.Exists(saved) || File.Exists(saved));
        await app.Copy($"after {id}");
        await Desk.QuitApp();
        await Desk.StartApp();
        await app.Prepare();
        await OpenPicker();
        Assert.DoesNotContain($"saved {id}", ReadPicker("history"));
        await Tap("Escape");
        await KeyUp("ctrl");
    }
}
