// ail-derived-from: CopyNotification@a9c7d5d9b4934557
// End-to-end check for CopyNotification: the small "Copied" notification, seen as a window titled "Shapeshifter"
// smaller than the picker. Its text is not read here (there is no OCR on the test desktop); its place, size and
// timing are.
using static Shapeshifter.Checks.CaptureDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class CopyNotificationChecks(CaptureAppFixture target) : Scenario, IClassFixture<CaptureAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); await WaitForNoNotification(); PointerAway(); }

    /// <summary>Copies without the target app's usual wait, so the notification is watched from the start.</summary>
    async Task<List<Sighting>> CopyAndWatch(string text, int ms = 3000)
    {
        await CopyNow(App, text);
        return await WatchNotifications(ms);
    }

    [Fact(DisplayName = "a copy shows the Copied notification at the bottom centre for a moment", Timeout = 30_000)]
    public async Task CopyShowsTheNotification()
    {
        var seen = await CopyAndWatch($"notify {Unique()}");
        Assert.NotEmpty(seen);
        var (first, last) = (seen[0], seen[^1]);
        Assert.True(first.At < 800, $"the notification appeared after {first.At} ms");
        Assert.False(IsTip(first.Box));
        // A single line, centred, just above the bottom of the usable screen.
        var area = WorkArea();
        var centre = first.Box.X + first.Box.Width / 2.0;
        Assert.True(Math.Abs(centre - (area.X + area.Width / 2.0)) <= 2, $"the notification's centre is at {centre}, the screen's at {area.X + area.Width / 2.0}");
        Assert.Equal(8, area.Y + area.Height - (first.Box.Y + first.Box.Height));
        // Up for about a second and a half.
        var shown = last.At - first.At;
        Assert.InRange(shown, 1101, 2099);
        Assert.Null(Notification());
    }

    [Fact(DisplayName = "copying the same thing twice in a row shows it only once", Timeout = 30_000)]
    public async Task SameThingTwiceShowsOnce()
    {
        var text = $"twice {Unique()}";
        Assert.NotEmpty(await CopyAndWatch(text));
        await WaitForNoNotification();
        Assert.Empty(await CopyAndWatch(text));
    }

    [Fact(DisplayName = "pointing at the notification makes it go away", Timeout = 30_000)]
    public async Task PointingMakesItGoAway()
    {
        await CopyNow(App, $"point {Unique()}");
        var box = Notification();
        for (var until = DateTime.UtcNow.AddMilliseconds(1500); box is null && DateTime.UtcNow < until; await Sleep(30)) box = Notification();
        Assert.NotNull(box);
        Xdotool("mousemove", (box.X + 20).ToString(System.Globalization.CultureInfo.InvariantCulture),
            (box.Y + box.Height / 2.0).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await Sleep(300);
        Assert.Null(Notification());
    }

    [Fact(DisplayName = "with Copy notifications off, a copy shows nothing", Timeout = 60_000)]
    public async Task OffShowsNothing()
    {
        await SetSwitch("notifications", false);
        try
        {
            PointerAway();
            Assert.Empty(await CopyAndWatch($"quiet {Unique()}"));
        }
        finally
        {
            await SetSwitch("notifications", true);
        }
        PointerAway();
        Assert.NotEmpty(await CopyAndWatch($"loud again {Unique()}"));
    }
}
