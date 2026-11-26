// ail-derived-from: PickFromHistory@88f45b41e4640889
// End-to-end check for PickFromHistory: choosing an older item with the keys already held, on a real X11 desktop,
// seen only through what the user sees.
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class PickFromHistoryChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    async Task CopyAll(params string[] texts) { foreach (var t in texts) await App.Copy(t); }

    [Fact(DisplayName = "pressing V again picks the next older item", Timeout = 30_000)]
    public async Task VAgainPicksOlder()
    {
        var id = Unique();
        await CopyAll($"one {id}", $"two {id}", $"three {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("v");
        Assert.True(PickerVisible());
        Screen.Keep();
        await KeyUp("ctrl");
        Assert.False(await WaitForPicker(false));
        Assert.Equal($"two {id}", await App.WaitForText($"two {id}"));
    }

    [Fact(DisplayName = "the arrow keys move the choice", Timeout = 30_000)]
    public async Task ArrowKeysMoveTheChoice()
    {
        var id = Unique();
        await CopyAll($"red {id}", $"green {id}", $"blue {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("Down");
        await Tap("Down");
        await Tap("Up");
        Screen.Keep();
        await KeyUp("ctrl");
        Assert.Equal($"green {id}", await App.WaitForText($"green {id}"));
    }

    [Fact(DisplayName = "a number jumps to that item", Timeout = 30_000)]
    public async Task NumberJumpsToItem()
    {
        var id = Unique();
        await CopyAll($"first {id}", $"second {id}", $"third {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await KeyDown("v");
        await Tap("3");
        await KeyUp("v");
        Assert.True(await WaitForPicker(true));
        Screen.Keep();
        await KeyUp("ctrl");
        Assert.Equal($"first {id}", await App.WaitForText($"first {id}"));
    }

    [Fact(DisplayName = "Enter pastes right away", Timeout = 30_000)]
    public async Task EnterPastesRightAway()
    {
        var id = Unique();
        await CopyAll($"early {id}", $"late {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("Down");
        await Tap("Return");
        Assert.False(await WaitForPicker(false));
        Assert.Equal($"early {id}", await App.WaitForText($"early {id}"));
        await KeyUp("ctrl");
        await Sleep(500);
        Assert.Equal($"early {id}", await App.Text());
    }

    [Fact(DisplayName = "the pasted item moves to the top", Timeout = 30_000)]
    public async Task PastedItemMovesToTop()
    {
        var id = Unique();
        await CopyAll($"older {id}", $"newer {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("Down");
        await KeyUp("ctrl");
        Assert.Equal($"older {id}", await App.WaitForText($"older {id}"));
        await Sleep(300);
        await App.Prepare();
        await KeyDown("ctrl");
        await Tap("v");
        await KeyUp("ctrl");
        Assert.Equal($"older {id}", await App.WaitForText($"older {id}"));
    }

    [Fact(DisplayName = "Escape cancels without pasting", Timeout = 30_000)]
    public async Task EscapeCancels()
    {
        var id = Unique();
        await CopyAll($"keep {id}", $"latest {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        await Tap("v");
        await Tap("Escape");
        Assert.False(await WaitForPicker(false));
        await KeyUp("ctrl");
        await Sleep(700);
        Assert.Equal("", await App.Text());
    }
}
