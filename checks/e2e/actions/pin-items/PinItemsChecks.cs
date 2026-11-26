// ail-derived-from: PinItems@dc042e3d435cd0a3
// End-to-end check for PinItems: pinning and unpinning from the actions, and pinned items surviving a restart.
// Pinned items stay at the top for good, so every scenario runs on a desktop of its own (PrivateDesktop), where it
// may also quit Shapeshifter from its tray menu and start it again.
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class PinItemsChecks : PrivateDesktopScenario
{
    /// <summary>With the picker up on the item to (un)pin: the pin action is the last one, so Right then Up chooses it.</summary>
    static async Task PinOrUnpinChosen()
    {
        await Tap("Right");
        await Tap("Up");
        await Sleep(200);
        await KeyUp("ctrl");
        await Sleep(1200);
    }

    [Fact(DisplayName = "pinning keeps an item at the top", Timeout = 180_000)]
    public async Task PinningKeepsAtTop()
    {
        var id = ReadableId();
        var app = await SetUp();
        await app.Copy($"keep {id}");
        await app.Copy($"later {id}");
        await app.Prepare();
        await OpenPicker(1);
        await PinOrUnpinChosen();
        Assert.False(await WaitForPicker(false));
        Assert.Equal("", await app.Text());
        await app.Copy($"even later {id}");
        await app.Prepare();
        await OpenPicker();
        var history = ReadPicker("history", $"keep {id}", $"even later {id}");
        Assert.Contains($"keep {id}", history);
        Assert.True(history.IndexOf($"keep {id}", StringComparison.Ordinal) < history.IndexOf($"even later {id}", StringComparison.Ordinal), history);
        await KeyUp("ctrl");
        Assert.Equal($"even later {id}", await app.WaitForText($"even later {id}"));
        await app.Prepare();
        await OpenPicker();
        await Tap("1");
        await KeyUp("ctrl");
        Assert.Equal($"keep {id}", await app.WaitForText($"keep {id}"));
    }

    [Fact(DisplayName = "a pinned item offers Unpin, and unpinning lets it move down again", Timeout = 180_000)]
    public async Task PinnedOffersUnpin()
    {
        var id = ReadableId();
        var app = await SetUp();
        await app.Copy($"sticky {id}");
        await app.Prepare();
        await OpenPicker();
        await PinOrUnpinChosen();
        await app.Copy($"next {id}");
        await OpenPicker();
        await Tap("1");
        await Tap("Right");
        await Sleep(300);
        Assert.Equal(["Paste", "Paste as file", "Unpin from clipboard"], ActionTitlesOnScreen());
        await Tap("Up");
        await KeyUp("ctrl");
        await Sleep(1200);
        await app.Copy($"newest {id}");
        await app.Prepare();
        await OpenPicker();
        var history = ReadPicker("history", $"newest {id}", $"sticky {id}");
        Assert.Contains($"newest {id}", history);
        Assert.True(history.IndexOf($"newest {id}", StringComparison.Ordinal) < history.IndexOf($"sticky {id}", StringComparison.Ordinal), history);
        await Tap("1");
        await KeyUp("ctrl");
        Assert.Equal($"newest {id}", await app.WaitForText($"newest {id}"));
    }

    [Fact(DisplayName = "pinned items come back after a restart, the rest does not", Timeout = 240_000)]
    public async Task PinnedComeBack()
    {
        var id = ReadableId();
        var app = await SetUp();
        await app.Copy($"pinned {id}");
        await app.Prepare();
        await OpenPicker();
        await PinOrUnpinChosen();
        await app.Copy($"gone {id}");
        await app.Copy($"current {id}");
        await Desk!.QuitApp();
        await Desk.StartApp();
        await app.Prepare();
        await OpenPicker();
        Assert.Contains($"pinned {id}", ReadPicker("history", $"pinned {id}"));
        Assert.DoesNotContain($"gone {id}", ReadPicker("history"));
        await Tap("1");
        await KeyUp("ctrl");
        Assert.Equal($"pinned {id}", await app.WaitForText($"pinned {id}"));
    }

    [Fact(DisplayName = "a deleted pinned item does not come back", Timeout = 240_000)]
    public async Task DeletedPinnedDoesNotComeBack()
    {
        var id = ReadableId();
        var app = await SetUp();
        await app.Copy($"doomed {id}");
        await app.Prepare();
        await OpenPicker();
        await PinOrUnpinChosen();
        await app.Copy($"other {id}");
        await OpenPicker();
        await Tap("1");
        await Tap("Delete");
        await Tap("Escape");
        await KeyUp("ctrl");
        await Desk!.QuitApp();
        await Desk.StartApp();
        await OpenPicker();
        Assert.DoesNotContain($"doomed {id}", ReadPicker("history"));
        await Tap("Escape");
        await KeyUp("ctrl");
    }
}
