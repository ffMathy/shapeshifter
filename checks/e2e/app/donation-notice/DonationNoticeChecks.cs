// ail-derived-from: DonationNotice@1f3df3b54c542296
// End-to-end check for DonationNotice: its scenarios, each played on a desktop of its own with a Shapeshifter started
// for it. Every other check runs with SPONSORKIT_DISABLE set (scripts/ail/run-legacy.sh); these pass it empty, and a
// SPONSORKIT_MOCK preset in place of the user's donations, so nothing reaches the network. The notice is found as a
// user finds it: a window on screen, its words read with OCR, its buttons clicked with the pointer.
using System.Globalization;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class DonationNoticeChecks : Scenario
{
    const string Title = "Support Shapeshifter";

    /// <summary>A desktop with a Shapeshifter that sees <paramref name="donations"/> as the user's Sponsorkit donations.</summary>
    static Task<OwnDesktop> Start(string donations, bool storeCopy = false)
    {
        var env = new Dictionary<string, string> { ["SPONSORKIT_DISABLE"] = "", ["SPONSORKIT_MOCK"] = donations };
        if (storeCopy) env["SHAPESHIFTER_STORE_COPY"] = "1";
        return OwnDesktop.Start(1280, 800, "donation-" + donations + (storeCopy ? "-store" : ""), env);
    }

    [Fact(DisplayName = "a copy without a monthly donation asks for one when it starts", Timeout = 180_000)]
    public async Task AsksWhenItStarts()
    {
        var desktop = await Start("none");
        try
        {
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, true), "the notice did not appear");
            var text = DonationDriver.Read(desktop.Display);
            Assert.Contains(Title, text, StringComparison.Ordinal);
            foreach (var part in new[] { "donating is optional", "monthly donation supports the project", "keeps it running", "removes this message when Shapeshifter starts" })
                Assert.Contains(part, text, StringComparison.Ordinal);
            Assert.Contains("Donate monthly", text, StringComparison.Ordinal);
            Assert.Contains("I already donate", text, StringComparison.Ordinal);
            Assert.Contains("Not now", text, StringComparison.Ordinal);
            Assert.True(DonationDriver.IsNear(desktop.Display, "bottom"), "the notice is not where the system's notifications appear");
            Screen.Keep(desktop.Display);
            // It stays: still there well after a notification would have gone.
            await Sleep(4000);
            Assert.True(DonationDriver.NoticeVisible(desktop.Display), "the notice went away by itself");
        }
        finally { await desktop.Stop(); }
    }

    [Fact(DisplayName = "Not now closes the notice", Timeout = 180_000)]
    public async Task NotNow()
    {
        var desktop = await Start("none");
        try
        {
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, true), "the notice did not appear");
            await DonationDriver.Click(desktop.Display, "Not now");
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, false), "the notice is still on screen");
        }
        finally { await desktop.Stop(); }
    }

    [Fact(DisplayName = "donating monthly closes the notice", Timeout = 180_000)]
    public async Task DonatingMonthly()
    {
        // The mock completes the checkout with a monthly donation at once.
        var desktop = await Start("none");
        try
        {
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, true), "the notice did not appear");
            await DonationDriver.Click(desktop.Display, "Donate monthly");
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, false), "the notice is still on screen");
        }
        finally { await desktop.Stop(); }
    }

    [Fact(DisplayName = "I already donate keeps the notice when no monthly donation is found", Timeout = 180_000)]
    public async Task AlreadyDonateWithoutDonation()
    {
        // The mock signs in at once, to an account without donations.
        var desktop = await Start("none");
        try
        {
            Assert.True(await DonationDriver.WaitForNotice(desktop.Display, true), "the notice did not appear");
            await DonationDriver.Click(desktop.Display, "I already donate");
            await Sleep(2000);
            Assert.True(DonationDriver.NoticeVisible(desktop.Display), "the notice closed without a monthly donation");
            Screen.Keep(desktop.Display);
        }
        finally { await desktop.Stop(); }
    }

    [Fact(DisplayName = "a monthly donor is not asked", Timeout = 180_000)]
    public async Task MonthlyDonor()
    {
        var desktop = await Start("active-monthly");
        try
        {
            Assert.False(await DonationDriver.WaitForNotice(desktop.Display, true, 12_000), "the notice appeared for a monthly donor");
        }
        finally { await desktop.Stop(); }
    }

    [Fact(DisplayName = "a copy from an app store never asks", Timeout = 180_000)]
    public async Task StoreCopy()
    {
        var desktop = await Start("none", storeCopy: true);
        try
        {
            Assert.False(await DonationDriver.WaitForNotice(desktop.Display, true, 12_000), "the notice appeared in a store copy");
        }
        finally { await desktop.Stop(); }
    }
}

/// <summary>Finds the donation notice on a display, reads it with OCR and clicks its buttons.</summary>
public static class DonationDriver
{
    static Dictionary<string, string> On(string display) => new() { ["DISPLAY"] = display };

    static string[] Windows(string display) =>
        Run("xdotool", ["search", "--onlyvisible", "--name", "^Support Shapeshifter$"], On(display)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public static bool NoticeVisible(string display) => Windows(display).Length > 0;

    /// <summary>Waits until the notice is on screen (or gone); true when it got there in time.</summary>
    public static async Task<bool> WaitForNotice(string display, bool shown, int timeout = 30_000)
    {
        for (var until = DateTime.UtcNow.AddMilliseconds(timeout); DateTime.UtcNow < until; await Sleep(250))
        {
            if (NoticeVisible(display) == shown) { await Sleep(shown ? 600 : 0); return true; }
        }
        return false;
    }

    static Box Geometry(string display, string id)
    {
        var info = Run("xwininfo", ["-id", id], On(display));
        int Num(string label)
        {
            var m = System.Text.RegularExpressions.Regex.Match(info, $@"{label}:\s*(-?\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }
        return new(Num("Absolute upper-left X"), Num("Absolute upper-left Y"), Num("Width"), Num("Height"));
    }

    /// <summary>Whether the notice sits at the top or the bottom of the screen (800 tall).</summary>
    public static bool IsNear(string display, string edge)
    {
        var box = Geometry(display, Windows(display)[0]);
        return edge == "bottom" ? box.Y + box.Height > 600 : box.Y < 200;
    }

    const int Scale = 3;

    /// <summary>The notice's words, as OCR reads them: one line per line of the window, then joined with spaces.</summary>
    static (string Text, List<(string Word, int X, int Y)> Words) Ocr(string display)
    {
        var id = Windows(display).FirstOrDefault() ?? throw new InvalidOperationException("the notice is not on screen");
        var dir = Directory.CreateTempSubdirectory("ail-donation-").FullName;
        try
        {
            var shot = Path.Combine(dir, "notice.png");
            var big = Path.Combine(dir, "big.png");
            RunBytes("import", ["-window", id, shot], On(display));
            // White text on a dark card: bigger, grey and inverted reads best.
            Run("convert", [shot, "-resize", $"{Scale * 100}%", "-colorspace", "Gray", "-negate", big]);
            var tsv = Run("tesseract", [big, "-", "--psm", "6", "tsv"], new Dictionary<string, string> { ["OMP_THREAD_LIMIT"] = "1" });
            var words = new List<(string, int, int)>();
            foreach (var cells in tsv.Split('\n').Skip(1).Select(l => l.Split('\t')).Where(c => c.Length > 11 && c[11].Trim().Length > 0))
            {
                int N(int i) => int.Parse(cells[i], CultureInfo.InvariantCulture);
                // OCR reads a lone capital I as a bar.
                var word = cells[11].Trim() is "|" or "l" ? "I" : cells[11].Trim();
                words.Add((word, (N(6) + N(8) / 2) / Scale, (N(7) + N(9) / 2) / Scale));
            }
            return (string.Join(' ', words.Select(w => w.Item1)), words);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    /// <summary>Everything the notice says, as one line.</summary>
    public static string Read(string display) => Ocr(display).Text;

    /// <summary>Clicks the button whose label is <paramref name="label"/>, in the middle of its words.</summary>
    public static async Task Click(string display, string label)
    {
        var id = Windows(display)[0];
        var box = Geometry(display, id);
        var (_, words) = Ocr(display);
        var parts = label.Split(' ');
        for (var i = 0; i + parts.Length <= words.Count; i++)
        {
            if (!parts.Select((p, k) => words[i + k].Word == p).All(match => match)) continue;
            var x = box.X + (words[i].X + words[i + parts.Length - 1].X) / 2;
            var y = box.Y + words[i].Y;
            Run("xdotool", ["mousemove", x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture), "click", "1"], On(display));
            await Sleep(500);
            return;
        }
        throw new InvalidOperationException($"no \"{label}\" button in the notice: {string.Join(' ', words.Select(w => w.Word))}");
    }
}
