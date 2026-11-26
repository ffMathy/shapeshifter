// ail-derived-from: OpenPicker@bce9a81188a5f8ed
// End-to-end check for OpenPicker: where the picker appears, that it leaves the keyboard with the app the user is in,
// and what happens on a screen too short for it. Seen only through the windows on screen and what they show.
using static Shapeshifter.Checks.PickerDriver;

namespace Shapeshifter.Checks;

public sealed class OpenPickerChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    /// <summary>
    /// Where a picker of <paramref name="size"/> belongs in the screen's working area (the screen less its panels):
    /// centred, its top 16 % down, or higher so that it ends at the bottom.
    /// </summary>
    static (int X, int Y) PlaceFor(Box size)
    {
        var area = WorkArea();
        return (
            area.X + (int)Math.Floor(Math.Max(0, (area.Width - size.Width) / 2.0)),
            area.Y + (int)Math.Floor(Math.Max(0, Math.Min(area.Height * 0.16, area.Height - size.Height))));
    }

    /// <summary>Shows the picker twice for the same history and returns the second one, so it is the size it was the time before.</summary>
    static async Task<Box> OpenTwice(string? display = null)
    {
        await HoldThePickerOpen(display);
        await CancelThePicker(display);
        return await HoldThePickerOpen(display);
    }

    [Fact(DisplayName = "the picker opens centred near the top of the screen", Timeout = 30_000)]
    public async Task OpensCentredNearTheTop()
    {
        await App.Copy($"centred {Desktop.Unique()}");
        await App.Prepare();
        MovePointer(200, 650);
        var box = await OpenTwice();
        await CancelThePicker();
        var place = PlaceFor(box);
        Assert.InRange(Math.Abs(box.X - place.X), 0, 1);
        var area = WorkArea();
        Assert.InRange(Math.Abs(box.Y - (area.Y + (int)Math.Floor(area.Height * 0.16))), 0, 1);
    }

    [Fact(DisplayName = "the picker never takes the keyboard focus", Timeout = 30_000)]
    public async Task NeverTakesTheFocus()
    {
        await App.Copy($"focus {Desktop.Unique()}");
        await App.Prepare();
        var before = FocusedWindowName();
        await HoldThePickerOpen();
        Assert.Equal(before, FocusedWindowName());
        await CancelThePicker();
        Assert.Equal(before, FocusedWindowName());
    }

    [Fact(DisplayName = "a picker too tall for its usual place moves up to end at the bottom of the screen", Timeout = 30_000)]
    public async Task TooTallMovesUp()
    {
        await CopyWithXclip(MakeImage(640, 400), "image/png");
        await App.Prepare();
        var box = await OpenTwice();
        await CancelThePicker();
        var area = WorkArea();
        Assert.True(box.Height > area.Height * 0.84, $"the picker is {box.Height} high, not more than 84 % of {area.Height}");
        Assert.InRange(Math.Abs(box.Y + box.Height - (area.Y + area.Height)), 0, 1);
        Assert.InRange(Math.Abs(box.X - PlaceFor(box).X), 0, 1);
    }

    [Fact(DisplayName = "the picker is placed for the size it had the time before", Timeout = 30_000)]
    public async Task PlacedForThePreviousSize()
    {
        await CopyWithXclip(MakeImage(640, 400), "image/png");
        await App.Prepare();
        var tall = await OpenTwice();
        await CancelThePicker();
        await App.Copy($"short {Desktop.Unique()}");
        await App.Prepare();
        var shortOne = await HoldThePickerOpen();
        await CancelThePicker();
        Assert.True(shortOne.Height < tall.Height, $"the short picker is {shortOne.Height} high, the tall one {tall.Height}");
        Assert.InRange(Math.Abs(shortOne.Y - PlaceFor(tall).Y), 0, 1);
    }

    [Fact(DisplayName = "on a screen too short for the picker, it is shown in turn whole without its preview and cut off with it", Timeout = 180_000)]
    public async Task OnAShortScreen()
    {
        var desktop = await OwnDesktop.Start(1280, 480, "short-screen");
        try
        {
            var d = desktop.Display;
            await CopyWithXclip("first short line\nsecond short line\nthird short line", null, d);
            var seen = new List<string>();
            for (var i = 0; i < 4; i++)
            {
                var box = await HoldThePickerOpen(d);
                var text = Squash(ReadText(ShootPicker(box, d)));
                await CancelThePicker(d);
                var preview = text.Contains("51 characters", StringComparison.Ordinal);
                var footer = text.Contains("Esc", StringComparison.Ordinal);
                if (!preview && footer && box.Height < 480) seen.Add("whole, without its preview");
                else if (preview && !footer && box.Y + box.Height >= 480) seen.Add("cut off, with its preview");
                else seen.Add($"neither ({box.Height} high at {box.Y}, preview {preview}, footer {footer})");
            }
            Assert.Contains("whole, without its preview", seen);
            Assert.Contains("cut off, with its preview", seen);
            Assert.DoesNotContain(seen, s => s.StartsWith("neither", StringComparison.Ordinal));
        }
        finally
        {
            await desktop.Stop();
        }
    }
}
