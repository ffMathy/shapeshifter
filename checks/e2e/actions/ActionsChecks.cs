// ail-derived-from: Actions@adb0f4c4431849b6
// End-to-end check for Actions: opening the actions of an item with Right, going back with Left, and running the
// chosen action by letting go of Ctrl. It sees only the text that lands in another app and the clipboard (xclip).
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class ActionsChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    [Fact(DisplayName = "Right opens the actions, and letting go of Ctrl runs the chosen one", Timeout = 30_000)]
    public async Task RightOpensTheActions()
    {
        var id = Unique();
        await App.Copy($"memo {id}");
        await App.Prepare();
        await OpenPicker();
        await Tap("Right");
        await Tap("Down");
        await KeyUp("ctrl");
        Assert.False(await WaitForPicker(false));
        await Sleep(1500);
        Assert.Equal("", await App.Text());
        var files = ClipboardFiles();
        Assert.Single(files);
        Assert.Matches(@"/Text \d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d\.txt$", files[0]);
        Assert.Equal($"memo {id}", await File.ReadAllTextAsync(files[0], TestContext.Current.CancellationToken));
    }

    [Fact(DisplayName = "Left goes back to the history", Timeout = 30_000)]
    public async Task LeftGoesBack()
    {
        var id = Unique();
        foreach (var t in new[] { "ant", "bee", "cat" }) await App.Copy($"{t} {id}");
        await App.Prepare();
        await OpenPicker();
        await Tap("Right");
        await Tap("Down");
        await Tap("Left");
        await Tap("Down");
        await KeyUp("ctrl");
        Assert.Equal($"bee {id}", await App.WaitForText($"bee {id}"));
    }

    [Fact(DisplayName = "choosing another item starts again at Paste", Timeout = 30_000)]
    public async Task AnotherItemStartsAtPaste()
    {
        var id = Unique();
        foreach (var t in new[] { "owl", "fox" }) await App.Copy($"{t} {id}");
        await App.Prepare();
        await OpenPicker();
        await Tap("Right");
        await Tap("Down");
        await Tap("v");
        await KeyUp("ctrl");
        Assert.Equal($"owl {id}", await App.WaitForText($"owl {id}"));
    }

    [Fact(DisplayName = "Escape in the actions cancels without running anything", Timeout = 30_000)]
    public async Task EscapeCancels()
    {
        var id = Unique();
        await App.Copy($"calm {id}");
        var before = ClipboardFormats();
        await App.Prepare();
        await OpenPicker();
        await Tap("Right");
        await Tap("Down");
        await Tap("Escape");
        await KeyUp("ctrl");
        await Sleep(1000);
        Assert.Equal("", await App.Text());
        Assert.Equal(before, ClipboardFormats());
    }
}
