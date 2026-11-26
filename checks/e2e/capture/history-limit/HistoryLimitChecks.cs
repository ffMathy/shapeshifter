// ail-derived-from: HistoryLimit@0ae42c498412fed2
// End-to-end check for HistoryLimit: how many items the history keeps, and pinned items on top. The limit is changed
// in the settings window, opened from the tray icon, as a user would.
using static Shapeshifter.Checks.CaptureDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class HistoryLimitChecks(CaptureAppFixture target) : Scenario, IClassFixture<CaptureAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); await WaitForNoNotification(); PointerAway(); }

    [Fact(DisplayName = "the history keeps the newest items, up to Items to remember", Timeout = 60_000)]
    public async Task KeepsUpToItemsToRemember()
    {
        await SetItemsToRemember(3);
        try
        {
            var id = Unique();
            foreach (var word in new[] { "first", "second", "third", "fourth" }) await App.Copy($"{word} {id}");
            // There is no fourth item: 4 does nothing, and the newest is pasted as usual.
            Assert.Equal($"fourth {id}", await PasteFromHistory(App, "4"));
            // The third item is the oldest one kept: "first" was dropped.
            Assert.Equal($"second {id}", await PasteFromHistory(App, "3"));
        }
        finally
        {
            await SetItemsToRemember(50);
        }
    }

    [Fact(DisplayName = "a pinned item stays on top and keeps its place", Timeout = 60_000)]
    public async Task PinnedItemStaysOnTop()
    {
        var id = Unique();
        await App.Copy($"filler {id}");
        await App.Copy($"pinned {id}");
        // Pin it: the history's actions are to the right; Pin is the last one.
        await Pick(["Right", "Up"], App.Prepare);
        try
        {
            await App.Copy($"newer {id}");
            await App.Copy($"newest {id}");
            // The choice still starts on the newest copy, so a plain paste is unchanged.
            Assert.Equal($"newest {id}", await PasteFromHistory(App));
            // The pinned item sits above it.
            Assert.Equal($"pinned {id}", await PasteFromHistory(App, "Up"));
            // Copying the pinned text again leaves it where it is: no second, unpinned copy appears under it.
            await App.Copy($"pinned {id}");
            Assert.Equal($"pinned {id}", await PasteFromHistory(App, "Up"));
            Assert.Equal($"newer {id}", await PasteFromHistory(App, "v"));
        }
        finally
        {
            // Unpin it again, so other checks start without pinned items.
            await Pick(["Up", "Right", "Up"], App.Prepare);
        }
    }
}
