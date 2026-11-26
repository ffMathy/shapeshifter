// ail-derived-from: CoachingTips@ed3a02e30186a25a
// End-to-end check for CoachingTips. A tip is the notification grown to a title over a wrapped sentence (taller than
// the one-line "Copied" one) and it stays up for about seven seconds. Tips are at least five minutes apart, so a check
// that expects one first waits until five minutes have passed since the last tip it saw; these checks therefore take
// several minutes.
using static Shapeshifter.Checks.CaptureDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class CoachingTipsChecks(CaptureAppFixture target) : Scenario, IClassFixture<CaptureAppFixture>
{
    const int Minutes = 60_000;

    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); await WaitForNoNotification(10_000); PointerAway(); }

    /// <summary>Copies in the target app (which has the focus) and watches the notification from that moment.</summary>
    async Task<List<Sighting>> CopyAndWatch(string text, int ms = 2500)
    {
        await App.Prepare();
        await CopyNow(App, text);
        return await WatchNotifications(ms);
    }

    static List<Sighting> Tips(List<Sighting> seen) => [.. seen.Where(s => IsTip(s.Box))];

    /// <summary>Copies "a", then "b", then "a" again: a habit the copied-again tip is about.</summary>
    async Task<List<Sighting>> CopyAgain(string id, int ms = 2500)
    {
        await App.Copy($"again a {id}");
        await App.Copy($"again b {id}");
        await WaitForNoNotification();
        return await CopyAndWatch($"again a {id}", ms);
    }

    [Fact(DisplayName = "copying the same thing twice in a row shows no tip", Timeout = 7 * Minutes)]
    public async Task SameThingTwiceShowsNoTip()
    {
        await WaitUntilATipCanShow();
        var text = $"double {Unique()}";
        await App.Copy(text);
        await WaitForNoNotification();
        Assert.Empty(await CopyAndWatch(text));
    }

    [Fact(DisplayName = "with Tips off, copying something again shows nothing", Timeout = 7 * Minutes)]
    public async Task TipsOffShowsNothing()
    {
        await WaitUntilATipCanShow();
        await SetSwitch("tips", false);
        try
        {
            PointerAway();
            Assert.Empty(await CopyAgain(Unique()));
        }
        finally
        {
            await SetSwitch("tips", true);
        }
    }

    [Fact(DisplayName = "copying something from the history again shows a tip", Timeout = 7 * Minutes)]
    public async Task CopyingAgainShowsATip()
    {
        await WaitUntilATipCanShow();
        var seen = await CopyAgain(Unique(), 9000);
        var shown = Tips(seen);
        Assert.NotEmpty(shown);
        SawTip();
        // It stays up for about seven seconds, in the notification's place at the bottom centre.
        Assert.True(shown[^1].At - shown[0].At > 6000, $"the tip was up for {shown[^1].At - shown[0].At} ms");
        Assert.Equal(shown.Count, seen.Count);
    }

    [Fact(DisplayName = "a second tip within five minutes is not shown", Timeout = 7 * Minutes)]
    public async Task SecondTipIsNotShown()
    {
        if (!TipShownLately())
        {
            await WaitUntilATipCanShow();
            Assert.NotEmpty(Tips(await CopyAgain(Unique(), 3000)));
            SawTip();
            await WaitForNoNotification(10_000);
        }
        // Another copy of something already in the history: neither a tip nor the "Copied" notification.
        Assert.Empty(await CopyAgain(Unique()));
    }

    [Fact(DisplayName = "copying back and forth between two apps shows a tip", Timeout = 7 * Minutes)]
    public async Task BackAndForthShowsATip()
    {
        await WaitUntilATipCanShow();
        using var other = await StartOtherApp();
        var id = Unique();
        await App.Prepare();
        await App.Copy($"from target {id}");
        await WaitForNoNotification();
        // Paste it into the other app with a quick Ctrl+V.
        await other.Focus();
        await KeyDown("ctrl");
        await Tap("v");
        await KeyUp("ctrl", 400);
        // Back in the first app, copy the next thing, well within 30 seconds.
        var seen = await CopyAndWatch($"next from target {id}", 3000);
        Assert.NotEmpty(Tips(seen));
        SawTip();
    }
}
