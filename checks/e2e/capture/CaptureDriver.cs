// Extra helpers for the end-to-end checks of capturing copies, the "Copied" notification and the tips. Like Desktop,
// it knows nothing of Shapeshifter's code: it copies with xclip as any X11 app would, reads the clipboard back with
// xclip, looks for small windows titled "Shapeshifter" (the notification) and clicks the settings window like a
// person would.
//
// Needs: DISPLAY, xdotool, xclip, xprop, xwininfo, ImageMagick (import, compare, convert), xlogo. The settings window
// opens from the tray icon, so changing a setting needs a StatusNotifierItem tray on the display:
// checks/e2e/app/tray-host.sh (an xfce4-panel), or status-notifier-watcher with gtk-sni-tray-standalone, on a D-Bus
// session that Shapeshifter was started with. Checks that change a setting fail with a clear message without one.
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

/// <summary>A moment, in milliseconds since watching began, when a notification was up, and its box.</summary>
public sealed record Sighting(long At, Box Box);

public static partial class CaptureDriver
{
    /// <summary>A folder of this run's own for the files the checks write.</summary>
    public static string Scratch { get; } = Directory.CreateTempSubdirectory("ail-capture-").FullName;

    static string Run(string file, params string[] args) => Desktop.Run(file, args);

    static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    // ---------------------------------------------------------------- the clipboard, as any other app

    /// <summary>Puts text on the clipboard under one type, as an app would.</summary>
    public static Task XCopy(string data, string type = "UTF8_STRING", int settle = 800) => XCopy(Encoding.UTF8.GetBytes(data), type, settle);

    /// <summary>
    /// Puts <paramref name="data"/> on the clipboard under one type, as an app would (xclip keeps owning the clipboard
    /// in the background until something else copies).
    /// </summary>
    public static async Task XCopy(byte[] data, string type = "UTF8_STRING", int settle = 800)
    {
        var file = Path.Combine(Scratch, $"in-{Unique()}");
        await File.WriteAllBytesAsync(file, data);
        // xclip forks a child that owns the selection; the parent returns at once. The child must not hold on to our
        // pipes, so it gets /dev/null for all three.
        Run("sh", "-c", "exec xclip -selection clipboard -t \"$1\" -i \"$2\" </dev/null >/dev/null 2>&1", "sh", type, file);
        await Sleep(settle);
    }

    /// <summary>The types the clipboard offers right now.</summary>
    public static string[] ClipboardTypes() => Lines(Run("xclip", "-selection", "clipboard", "-o", "-t", "TARGETS"));

    /// <summary>The clipboard's bytes for one type.</summary>
    public static byte[] ClipboardBytes(string type) => RunBytes("xclip", ["-selection", "clipboard", "-o", "-t", type]).Output;

    /// <summary>Whether two PNG files show exactly the same pixels.</summary>
    public static bool SamePixels(string a, string b)
    {
        var info = new ProcessStartInfo("compare") { RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false };
        foreach (var arg in new[] { "-metric", "AE", a, b, "null:" }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        _ = stdout.Result;
        return stderr.Trim() == "0";
    }

    // ---------------------------------------------------------------- the history, through the gesture

    /// <summary>
    /// With the target app focused: holds Ctrl+V until the picker appears, taps each key in <paramref name="keys"/>
    /// (e.g. ["v", "v"], ["Up"], ["3"]) and lets go of Ctrl. Returns false, having let go of everything, if the picker
    /// did not appear.
    /// </summary>
    static async Task<bool> TryPick(string[] keys, bool keep)
    {
        await KeyDown("ctrl");
        try
        {
            await KeyDown("v", 0);
            var shown = await WaitForPicker(true, 4000);
            await KeyUp("v");
            if (!shown) return false;
            foreach (var k in keys) await Tap(k, 120);
            if (keep) Screen.Keep();
            return true;
        }
        finally
        {
            await KeyUp("ctrl");
            await WaitForPicker(false);
        }
    }

    /// <summary>
    /// Picks from the history like a person who, when the history does not come up (the first time right after
    /// Shapeshifter starts), simply tries again. <paramref name="before"/> runs before each try (e.g. emptying the text box).
    /// </summary>
    public static async Task Pick(string[] keys, Func<Task>? before = null, bool keep = false)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            if (before is not null) await before();
            if (await TryPick(keys, keep)) return;
            await Sleep(1000);
        }
        throw new InvalidOperationException("the picker did not appear while Ctrl+V was held, three times");
    }

    /// <summary>Pastes what picking <paramref name="keys"/> from the history gives, into an empty text box, and returns it.</summary>
    public static async Task<string> PasteFromHistory(TargetApp app, params string[] keys)
    {
        await Pick(keys, app.Prepare);
        await Sleep(700);
        return await app.Text();
    }

    /// <summary>
    /// Like <see cref="PasteFromHistory"/>, but keeps the screen with the history open on the chosen item as the
    /// scenario's proof: for items whose paste shows nothing in a text box, such as files or an image.
    /// </summary>
    public static async Task<string> PasteFromHistoryShowingIt(TargetApp app, params string[] keys)
    {
        await Pick(keys, app.Prepare, keep: true);
        await Sleep(700);
        return await app.Text();
    }

    // ---------------------------------------------------------------- the notification

    static Box? Geometry(string id)
    {
        var info = Run("xwininfo", "-id", id);
        int? N(string label)
        {
            var m = Regex.Match(info, $@"{label}:\s*(-?\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;
        }
        var (x, y, width, height) = (N("Absolute upper-left X"), N("Absolute upper-left Y"), N("Width"), N("Height"));
        return height is null ? null : new Box(x ?? 0, y ?? 0, width ?? 0, height.Value);
    }

    /// <summary>
    /// The notification, if one is on screen: a visible window titled "Shapeshifter" smaller than the picker. A plain
    /// "Copied" one is a single line; a tip, a title over a wrapped sentence, is taller.
    /// </summary>
    public static Box? Notification()
    {
        foreach (var id in Lines(Xdotool("search", "--onlyvisible", "--name", "^Shapeshifter$")))
        {
            var box = Geometry(id);
            if (box is { Height: > 0 and < 200 }) return box;
        }
        return null;
    }

    public static bool IsTip(Box? box) => box is not null && box.Height >= 60;

    /// <summary>Watches the screen for <paramref name="ms"/> and returns every moment a notification was up, with its box.</summary>
    public static async Task<List<Sighting>> WatchNotifications(int ms)
    {
        var seen = new List<Sighting>();
        var clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < ms)
        {
            var box = Notification();
            if (box is not null)
            {
                if (seen.Count == 0) Screen.KeepSoon();
                seen.Add(new Sighting(clock.ElapsedMilliseconds, box));
            }
            await Sleep(40);
        }
        if (seen.Any(s => IsTip(s.Box))) SawTip();
        return seen;
    }

    /// <summary>Waits until no notification is on screen, so the next check starts from a clean screen.</summary>
    public static async Task WaitForNoNotification(int timeout = 9000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while (Notification() is not null && DateTime.UtcNow < until) await Sleep(100);
    }

    /// <summary>The usable part of the screen (without panels), from the window manager.</summary>
    public static Box WorkArea()
    {
        var m = WorkAreaPattern().Match(Run("xprop", "-root", "_NET_WORKAREA"));
        if (!m.Success) throw new InvalidOperationException("the window manager does not report a work area");
        int G(int i) => int.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
        return new Box(G(1), G(2), G(3), G(4));
    }

    // ---------------------------------------------------------------- tips are at least five minutes apart

    static readonly TimeSpan TipGap = TimeSpan.FromMilliseconds(5 * 60_000 + 5_000);

    // Checks of other areas that ran just before against the same app may have shown a tip without anyone watching,
    // so until a tip is seen here, assume one was shown the moment these checks started.
    static DateTime lastTipSeen = DateTime.UtcNow;

    /// <summary>Remembers that a tip was just seen (a check that saw one calls this).</summary>
    public static void SawTip() => lastTipSeen = DateTime.UtcNow;

    /// <summary>
    /// A tip only shows five minutes after the last one, so a check that expects a tip first waits until five minutes
    /// have passed since the last tip it saw.
    /// </summary>
    public static async Task WaitUntilATipCanShow()
    {
        var wait = lastTipSeen + TipGap - DateTime.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait);
    }

    /// <summary>Whether a tip was seen in the last five minutes, so a new one cannot show yet.</summary>
    public static bool TipShownLately() => DateTime.UtcNow - lastTipSeen < TimeSpan.FromMinutes(5);

    // ---------------------------------------------------------------- the settings window

    /// <summary>Opens the settings window from the tray icon and returns its client area.</summary>
    public static async Task<Box> OpenSettings()
    {
        var win = SettingsWindow();
        if (win is null)
        {
            var icon = TrayIcon() ?? throw new InvalidOperationException("no tray on this display: the settings window opens from Shapeshifter's tray icon (start checks/e2e/app/tray-host.sh, then Shapeshifter with its D-Bus address)");
            Xdotool("mousemove", Str(icon.X), Str(icon.Y), "click", "1");
            for (var i = 0; i < 50 && (win = SettingsWindow()) is null; i++) await Sleep(100);
            if (win is null) throw new InvalidOperationException("clicking the tray icon did not open the settings window");
        }
        await Sleep(500);
        return win;
    }

    static string Str(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Where Shapeshifter's tray icon is: the right end of the xfce4-panel that checks/e2e/app/tray-host.sh puts at the
    /// bottom of the screen, or the only icon of a gtk-sni-tray-standalone bar.
    /// </summary>
    static (int X, int Y)? TrayIcon()
    {
        IEnumerable<Box?> Find(string name) => Lines(Xdotool("search", "--onlyvisible", "--name", name)).Select(Geometry);
        var panel = Find("^xfce4-panel$").FirstOrDefault(g => g is { Width: > 200 });
        if (panel is not null) return (panel.X + panel.Width - 17, panel.Y + panel.Height / 2);
        var bar = Find("^gtk-sni-tray-standalone$").FirstOrDefault(g => g is not null);
        return bar is null ? null : (bar.X + 15, bar.Y + bar.Height / 2);
    }

    static Box? SettingsWindow()
    {
        var id = Lines(Xdotool("search", "--onlyvisible", "--name", "^Shapeshifter Settings$")).FirstOrDefault();
        return id is null ? null : Geometry(id);
    }

    public static async Task CloseSettings()
    {
        foreach (var id in Lines(Xdotool("search", "--name", "^Shapeshifter Settings$"))) Xdotool("windowclose", id);
        await Sleep(300);
    }

    // Rows of the settings window, as offsets from its top-left corner (the Linux look at 1×; the window is 540 wide).
    // The switches sit at the right end of their row; "Items to remember" has its number left of − and +.
    const int ItemsRow = 341;
    const int SwitchX = 473;
    const int NumberX = 407;

    static int RowOf(string name) => name switch
    {
        "notifications" => 510,
        "tips" => 569,
        _ => throw new ArgumentException($"no switch called {name}", nameof(name)),
    };

    static async Task Click(Box win, int dx, int dy)
    {
        Xdotool("mousemove", Str(win.X + dx), Str(win.Y + dy), "click", "1");
        await Sleep(300);
    }

    /// <summary>Whether a switch is on: an on switch is filled with the accent colour, an off one is light grey.</summary>
    static bool SwitchIsOn(Box win, int dy)
    {
        var pixel = Run("import", "-window", "root", "-crop", $"1x1+{win.X + SwitchX - 12}+{win.Y + dy}", "txt:-");
        var m = ColourPattern().Match(pixel);
        if (!m.Success) throw new InvalidOperationException("could not read the switch's colour");
        int C(int i) => int.Parse(m.Groups[i].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return C(3) - C(1) > 60; // the accent is blue; off is a neutral grey
    }

    /// <summary>Turns a switch of the settings window ("notifications" or "tips") on or off, like a person: by clicking it if it is not already so.</summary>
    public static async Task SetSwitch(string name, bool on)
    {
        var win = await OpenSettings();
        var row = RowOf(name);
        if (SwitchIsOn(win, row) != on) await Click(win, SwitchX, row);
        if (SwitchIsOn(win, row) != on) throw new InvalidOperationException($"the {name} switch did not turn {(on ? "on" : "off")}");
        await CloseSettings();
    }

    /// <summary>Types a number into "Items to remember".</summary>
    public static async Task SetItemsToRemember(int count)
    {
        var win = await OpenSettings();
        await Click(win, NumberX, ItemsRow);
        Xdotool("key", "ctrl+a");
        Xdotool("type", Str(count));
        Xdotool("key", "Tab");
        await Sleep(400);
        await CloseSettings();
    }

    // ---------------------------------------------------------------- small helpers shared by the checks

    /// <summary>Moves the pointer to a corner, so it never rests on the notification (pointing at it dismisses it).</summary>
    public static void PointerAway() => Xdotool("mousemove", "1270", "10");

    /// <summary>Copies text in the target app without its usual wait, so what follows can watch from that moment.</summary>
    public static async Task CopyNow(TargetApp app, string text) => Assert.Equal("OK", await app.Send($"copy-text {B64(text)}"));

    /// <summary>A second, different app (xlogo) that can be focused and closed.</summary>
    public sealed class OtherApp(Process process, string window) : IDisposable
    {
        public async Task Focus()
        {
            Xdotool("windowactivate", "--sync", window);
            await Sleep(300);
        }

        public void Dispose()
        {
            try { process.Kill(true); } catch (InvalidOperationException) { /* already gone */ }
            process.Dispose();
        }
    }

    /// <summary>Starts a second, different app (xlogo).</summary>
    public static async Task<OtherApp> StartOtherApp()
    {
        // xlogo does not say which process it is, so look for the XLogo window that was not there before.
        static string[] Windows() => Lines(Xdotool("search", "--onlyvisible", "--class", "XLogo"));
        var before = Windows().ToHashSet();
        var info = new ProcessStartInfo("xlogo") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in new[] { "-geometry", "200x200+700+100" }) info.ArgumentList.Add(arg);
        var process = Process.Start(info)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        var id = "";
        for (var i = 0; i < 50 && id.Length == 0; i++)
        {
            await Sleep(100);
            id = Windows().FirstOrDefault(w => !before.Contains(w)) ?? "";
        }
        if (id.Length == 0)
        {
            try { process.Kill(true); } catch (InvalidOperationException) { /* already gone */ }
            process.Dispose();
            throw new InvalidOperationException("xlogo did not open a window");
        }
        return new OtherApp(process, id);
    }

    [GeneratedRegex(@"=\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)")]
    private static partial Regex WorkAreaPattern();

    [GeneratedRegex("#([0-9A-F]{2})([0-9A-F]{2})([0-9A-F]{2})", RegexOptions.IgnoreCase)]
    private static partial Regex ColourPattern();
}

/// <summary>
/// One target app for all the scenarios of a capture check, as bun's beforeAll/afterAll did: started with the pointer
/// moved out of the notification's way, and stopped with the settings window closed.
/// </summary>
public sealed class CaptureAppFixture : IAsyncLifetime
{
    TargetApp? app;

    public TargetApp App => app ?? throw new InvalidOperationException("the target app has not started");

    public async ValueTask InitializeAsync()
    {
        app = await TargetApp.Start();
        CaptureDriver.PointerAway();
    }

    public async ValueTask DisposeAsync()
    {
        await Desktop.ReleaseAll();
        await CaptureDriver.CloseSettings();
        if (app is not null) await app.DisposeAsync();
    }
}
