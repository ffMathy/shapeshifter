// ail-derived-from: SearchHistory@aac74c1589e9545c
// End-to-end check for SearchHistory: S during the gesture, typing in the picker, then Enter, Escape or another
// window, on a real X11 desktop. It sees only what a user sees: which window has the keyboard focus, the text that
// lands in another app, files on disk, and the words on the picker.
using static Shapeshifter.Checks.Desktop;
using static Shapeshifter.Checks.SearchDriver;

namespace Shapeshifter.Checks;

public sealed class SearchHistoryChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    const string Target = "Shapeshifter E2E Target";

    TargetApp App => target.App;

    protected override async Task AfterEach()
    {
        await ReleaseAll();
        if (FocusedWindowName() == "Shapeshifter") await Tap("Escape");
        await WaitForPicker(false);
    }

    async Task CopyAll(params string[] texts) { foreach (var t in texts) await App.Copy(t); }

    [Fact(DisplayName = "S starts a search, even before the history appears", Timeout = 30_000)]
    public async Task SStartsASearch()
    {
        var id = Unique();
        await CopyAll($"pear {id}");
        await App.Prepare();
        await KeyDown("ctrl");
        await KeyDown("v", 30);
        await Tap("s", 30);
        await KeyUp("v");
        await KeyUp("ctrl");
        Assert.True(await WaitForPicker(true));
        Assert.True(await WaitForFocus("Shapeshifter"));
        // The caret stands on the placeholder's S, which OCR reads as another letter while the caret shows.
        var words = await WaitForPickerWords(["earch everything you copied"]);
        Assert.Contains("earch everything you copied", words, StringComparison.Ordinal);
        Assert.Equal("", await App.Text());
    }

    [Fact(DisplayName = "typing narrows the history, and Enter pastes into the app the user came from", Timeout = 30_000)]
    public async Task EnterPastesIntoTheAppTheUserCameFrom()
    {
        var id = Unique();
        await CopyAll($"plum {id}", $"fig {id}", $"lime {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"fig {id}");
        Assert.Equal("", await App.Text());
        await Tap("Return");
        Assert.False(await WaitForPicker(false));
        Assert.True(await WaitForFocus(Target));
        Assert.Equal($"fig {id}", await App.WaitForText($"fig {id}"));
    }

    [Fact(DisplayName = "the arrows move through the results, wrapping around", Timeout = 30_000)]
    public async Task ArrowsWrapAround()
    {
        var id = Unique();
        await CopyAll($"kiwi red {id}", $"kiwi green {id}", $"kiwi blue {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"kiwi {id}");
        await Tap("Up");
        await Tap("Return");
        Assert.Equal($"kiwi red {id}", await App.WaitForText($"kiwi red {id}"));
    }

    [Fact(DisplayName = "Right at the end of the query opens the actions", Timeout = 30_000)]
    public async Task RightAtTheEndOpensTheActions()
    {
        var id = Unique();
        await CopyAll($"memo {id}", $"other {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"memo {id}");
        await Tap("Right");
        await Tap("Down");
        await Tap("Return");
        Assert.False(await WaitForPicker(false));
        // The second action, "Paste as file": the clipboard now holds a .txt file with the text.
        var uris = "";
        for (var until = DateTime.UtcNow.AddMilliseconds(4000); DateTime.UtcNow < until && !uris.Contains(".txt", StringComparison.Ordinal); await Sleep(200))
        {
            if (ClipboardTargets().Contains("text/uri-list")) uris = ClipboardAs("text/uri-list");
        }
        var first = uris.Trim().Split('\n')[0].TrimEnd('\r');
        var path = Uri.UnescapeDataString(first.StartsWith("file://", StringComparison.Ordinal) ? first[7..] : first);
        Assert.EndsWith(".txt", path, StringComparison.Ordinal);
        Assert.Equal($"memo {id}", File.ReadAllText(path).TrimStart('﻿'));
        // A copy made within a moment of "Paste as file" is not kept in the history (not this check's subject): wait.
        await Sleep(2000);
    }

    [Fact(DisplayName = "Right inside the query moves the caret instead", Timeout = 30_000)]
    public async Task RightInsideMovesTheCaret()
    {
        var id = Unique();
        await CopyAll($"note {id}", $"other {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"note {id}");
        await Tap("Left");
        await Tap("Right");
        await Tap("Return");
        Assert.Equal($"note {id}", await App.WaitForText($"note {id}"));
    }

    [Fact(DisplayName = "Escape gives the focus back without pasting", Timeout = 30_000)]
    public async Task EscapeGivesTheFocusBack()
    {
        var id = Unique();
        await CopyAll($"first {id}", $"second {id}");
        await App.Prepare();
        await StartSearch();
        await Type("first");
        await Tap("Escape");
        Assert.False(await WaitForPicker(false));
        Assert.True(await WaitForFocus(Target));
        await Type("ok");
        Assert.Equal("ok", await App.WaitForText("ok"));
    }

    [Fact(DisplayName = "clicking another window cancels the search", Timeout = 30_000)]
    public async Task ClickingAnotherWindowCancels()
    {
        var id = Unique();
        await CopyAll($"apricot {id}");
        await App.Prepare();
        await StartSearch();
        await Type("apricot");
        // The text box of the other app, left of the picker.
        await ClickAt(100, 200);
        Assert.False(await WaitForPicker(false));
        Assert.Equal(Target, FocusedWindowName());
        await Sleep(500);
        Assert.Equal("", await App.Text());
    }

    [Fact(DisplayName = "Ctrl+V in the search box pastes nothing on Linux", Timeout = 30_000)]
    public async Task CtrlVInTheSearchBox()
    {
        var id = Unique();
        await CopyAll($"clip {id}");
        await App.Prepare();
        await StartSearch();
        await KeyDown("ctrl");
        await Tap("v");
        await KeyUp("ctrl");
        await Sleep(500);
        Assert.Equal("Shapeshifter", FocusedWindowName());
        Assert.Contains("earch everything you copied", await WaitForPickerWords(["earch everything you copied"]), StringComparison.Ordinal);
        await Tap("Escape");
        await Sleep(500);
        Assert.Equal("", await App.Text());
    }
}
