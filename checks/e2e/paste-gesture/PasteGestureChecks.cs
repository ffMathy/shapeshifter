// ail-derived-from: PasteGesture@c39fdb090a7b5b8b
// End-to-end check for PasteGesture: its scenarios, played with real keys on an X11 desktop against the running app.
// It sees only what a user sees: the text that lands in another app, and whether the picker is on screen.
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class PasteGestureChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    [Fact(DisplayName = "a quick Ctrl+V pastes what was copied last", Timeout = 20_000)]
    public async Task QuickPaste()
    {
        var text = $"alpha {Unique()}";
        await App.Copy(text);
        await App.Prepare();
        await KeyDown("ctrl");
        await Tap("v");
        Assert.False(PickerVisible());
        await KeyUp("ctrl");
        Assert.Equal(text, await App.WaitForText(text));
        Assert.False(PickerVisible());
    }

    [Fact(DisplayName = "keeping Ctrl held, each tap of V pastes again", Timeout = 20_000)]
    public async Task EachTapPastesAgain()
    {
        var text = $"again {Unique()}";
        await App.Copy(text);
        await App.Prepare();
        await KeyDown("ctrl");
        await Tap("v");
        Assert.Equal(text, await App.WaitForText(text));
        await Tap("v");
        Assert.Equal(text + text, await App.WaitForText(text + text));
        Assert.False(PickerVisible());
        await KeyUp("ctrl");
        await Sleep(500);
        Assert.Equal(text + text, await App.Text());
    }

    [Fact(DisplayName = "letting go of Ctrl a moment before V still pastes", Timeout = 20_000)]
    public async Task RollingRelease()
    {
        var text = $"rolling {Unique()}";
        await App.Copy(text);
        await App.Prepare();
        await KeyDown("ctrl");
        await KeyDown("v", 40);
        await KeyUp("ctrl");
        await KeyUp("v");
        Assert.Equal(text, await App.WaitForText(text));
    }

    [Fact(DisplayName = "typing V without Ctrl types a v", Timeout = 20_000)]
    public async Task PlainV()
    {
        await App.Prepare();
        await Tap("v");
        await Tap("v");
        Assert.Equal("vv", await App.WaitForText("vv"));
    }

    [Fact(DisplayName = "Ctrl+Shift+V is left to the app", Timeout = 20_000)]
    public async Task CtrlShiftV()
    {
        await App.Copy($"unrelated {Unique()}");
        await App.Prepare();
        await KeyDown("ctrl");
        await KeyDown("shift");
        await KeyDown("v", 0);
        var appeared = false;
        for (var until = DateTime.UtcNow.AddMilliseconds(700); DateTime.UtcNow < until; await Sleep(50)) appeared |= PickerVisible();
        await KeyUp("v");
        await KeyUp("shift");
        await KeyUp("ctrl");
        Assert.False(appeared);
    }

    [Fact(DisplayName = "holding Ctrl+V shows the history", Timeout = 20_000)]
    public async Task HoldShowsHistory()
    {
        var id = Unique();
        await App.Copy($"first {id}");
        await App.Copy($"newest {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        Assert.True(PickerVisible());
        Screen.Keep();
        await KeyUp("ctrl");
        Assert.False(await WaitForPicker(false));
        Assert.Equal($"newest {id}", await App.WaitForText($"newest {id}"));
    }
}
