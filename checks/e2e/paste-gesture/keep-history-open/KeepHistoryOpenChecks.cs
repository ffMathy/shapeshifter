// ail-derived-from: KeepHistoryOpen@0a094410a23655fe
// End-to-end check for KeepHistoryOpen: Space keeps the picker up without holding Ctrl, on a real X11 desktop, seen
// only through what the user sees.
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class KeepHistoryOpenChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await Tap("Escape"); await ReleaseAll(); await WaitForPicker(false); }

    static async Task KeepOpen()
    {
        await KeyDown("ctrl");
        await KeyDown("v");
        await Tap("space");
        await KeyUp("v");
        Assert.True(await WaitForPicker(true));
        await KeyUp("ctrl");
    }

    [Fact(DisplayName = "Space keeps the history open, and Enter pastes", Timeout = 30_000)]
    public async Task SpaceKeepsItOpen()
    {
        var id = Unique();
        foreach (var t in new[] { $"oak {id}", $"elm {id}", $"ash {id}" }) await App.Copy(t);
        await App.Prepare();
        await KeepOpen();
        await Sleep(400);
        Assert.True(PickerVisible());
        await Tap("Down");
        await Tap("Down");
        await Tap("2");
        Screen.Keep();
        await Tap("Return");
        Assert.Equal($"elm {id}", await App.WaitForText($"elm {id}"));
        Assert.False(await WaitForPicker(false));
    }

    [Fact(DisplayName = "Escape closes a history kept open, without pasting", Timeout = 30_000)]
    public async Task EscapeCloses()
    {
        var id = Unique();
        foreach (var t in new[] { $"stay {id}", $"newest {id}" }) await App.Copy(t);
        await App.Prepare();
        await KeepOpen();
        await Tap("Escape");
        Assert.False(await WaitForPicker(false));
        await Sleep(700);
        Assert.Equal("", await App.Text());
    }
}
