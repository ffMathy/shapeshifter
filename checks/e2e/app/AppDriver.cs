// Drives the parts of Shapeshifter around the paste gesture the way a person would: the tray icon and its menu, the
// settings window, the "Copied" notification, and what Shapeshifter opens in the browser or the file manager.
// It knows nothing of Shapeshifter's code. It needs, on the same DISPLAY as Shapeshifter:
// - a system tray: checks/e2e/app/tray-host.sh starts one (a session D-Bus and a panel at the bottom of the screen);
//   Shapeshifter must be started after it, with its DBUS_SESSION_BUS_ADDRESS;
// - checks/e2e/app/bin first on Shapeshifter's PATH: its xdg-open stands in for the browser and the file manager and
//   writes what would have opened to .ail/browser/<display>.txt, so no real browser ever opens.
// Positions inside the settings window are those of the Linux look ("Boxed Sheet"), which has no "Shortcut" row.
using System.Globalization;
using System.Text.RegularExpressions;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public static partial class AppDriver
{
    static string[] VisibleWindows(string name) =>
        Xdotool("search", "--onlyvisible", "--name", name).Split('\n', StringSplitOptions.RemoveEmptyEntries);

    readonly record struct Geometry(int X, int Y, int Width, int Height);

    /// <summary>The absolute position and size of a window's content, without the title bar.</summary>
    static Geometry GeometryOf(string id)
    {
        var info = Run("xwininfo", ["-id", id]);
        int Num(string label)
        {
            var m = Regex.Match(info, $@"{label}:\s*(-?\d+)");
            return m.Success ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }
        return new(Num("Absolute upper-left X"), Num("Absolute upper-left Y"), Num("Width"), Num("Height"));
    }

    // ------------------------------------------------------------------ the tray

    /// <summary>Where Shapeshifter's icon is: the right end of the panel tray-host.sh puts at the bottom of the screen.</summary>
    static (int X, int Y) TrayIconPosition()
    {
        var panels = VisibleWindows("^xfce4-panel$").Select(GeometryOf).Where(g => g.Width > 200).ToList();
        if (panels.Count == 0)
            throw new InvalidOperationException("no system tray on this display: start checks/e2e/app/tray-host.sh, then Shapeshifter with its D-Bus address");
        var panel = panels[0];
        return (panel.X + panel.Width - 17, panel.Y + panel.Height / 2);
    }

    static string S(int n) => n.ToString(CultureInfo.InvariantCulture);

    /// <summary>Left-clicks Shapeshifter's tray icon.</summary>
    public static async Task ClickTrayIcon()
    {
        var (x, y) = TrayIconPosition();
        Xdotool("mousemove", S(x), S(y), "click", "1");
        await Sleep(300);
    }

    /// <summary>The tray menu's items, in order; the separators are skipped by the keyboard.</summary>
    public static readonly string[] TrayItems =
        ["Settings…", "Open log folder", "Clear history", "Check for updates…", "Report a problem…", "About Shapeshifter", "Quit Shapeshifter"];

    /// <summary>Opens the tray menu with a right click and chooses an item with the arrow keys and Enter.</summary>
    public static async Task ChooseFromTrayMenu(string item)
    {
        var index = Array.IndexOf(TrayItems, item);
        if (index < 0) throw new ArgumentException($"no \"{item}\" in the tray menu", nameof(item));
        var (x, y) = TrayIconPosition();
        Xdotool("mousemove", S(x), S(y), "click", "3");
        await Sleep(900);
        for (var i = 0; i <= index; i++) { Xdotool("key", "Down"); await Sleep(80); }
        Xdotool("key", "Return");
        await Sleep(400);
    }

    // ------------------------------------------------------------------ the settings window

    public const string SettingsTitle = "^Shapeshifter Settings$";

    public static string[] SettingsWindows() => VisibleWindows(SettingsTitle);

    public static async Task<bool> WaitForSettings(bool open, int timeout = 4000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while ((SettingsWindows().Length > 0) != open)
        {
            if (DateTime.UtcNow > until) return !open;
            await Sleep(100);
        }
        await Sleep(open ? 400 : 0);
        return open;
    }

    /// <summary>Opens the settings with the tray icon, or brings the open window forward.</summary>
    public static async Task OpenSettings()
    {
        if (SettingsWindows().Length == 0) await ClickTrayIcon();
        if (!await WaitForSettings(true)) throw new InvalidOperationException("the settings window did not open");
        Xdotool("windowactivate", "--sync", SettingsWindows()[0]);
        await Sleep(200);
    }

    /// <summary>Closes every settings window the way a user does, with Alt+F4.</summary>
    public static async Task CloseSettings()
    {
        foreach (var id in SettingsWindows())
        {
            Xdotool("windowactivate", "--sync", id);
            Xdotool("key", "alt+F4");
        }
        await WaitForSettings(false);
    }

    /// <summary>Where things are in the settings window, from the top left of its content (Linux look).</summary>
    public enum SettingsSpot
    {
        CopyVersion, ReportProblem, ProjectPage,
        Delay, ItemsToRemember, RememberHistory,
        CopyNotifications, Tips, StartAtLogin, AutomaticUpdates,
    }

    static (int X, int Y) Spot(SettingsSpot spot) => spot switch
    {
        SettingsSpot.CopyVersion => (242, 60),
        SettingsSpot.ReportProblem => (343, 60),
        SettingsSpot.ProjectPage => (441, 60),
        SettingsSpot.Delay => (403, 282),
        SettingsSpot.ItemsToRemember => (407, 341),
        SettingsSpot.RememberHistory => (473, 401),
        SettingsSpot.CopyNotifications => (473, 510),
        SettingsSpot.Tips => (473, 569),
        SettingsSpot.StartAtLogin => (473, 628),
        SettingsSpot.AutomaticUpdates => (473, 687),
        _ => throw new ArgumentOutOfRangeException(nameof(spot)),
    };

    public static async Task ClickInSettings(SettingsSpot spot)
    {
        var g = GeometryOf(SettingsWindows()[0]);
        var (dx, dy) = Spot(spot);
        Xdotool("mousemove", S(g.X + dx), S(g.Y + dy), "click", "1");
        await Sleep(400);
    }

    /// <summary>Types a number into "Delay before the history appears" or "Items to remember" and moves on with Tab.</summary>
    public static async Task TypeNumber(SettingsSpot field, int value)
    {
        if (field is not (SettingsSpot.Delay or SettingsSpot.ItemsToRemember)) throw new ArgumentException("not a number box", nameof(field));
        await ClickInSettings(field);
        Xdotool("key", "ctrl+a");
        await Sleep(100);
        Xdotool("type", "--delay", "40", S(value));
        Xdotool("key", "Tab");
        await Sleep(500);
    }

    /// <summary>The buttons below the settings, reached from the end of the window with Shift+Tab (they can be off a low screen).</summary>
    public enum SettingsButton { OpenLogFolder = 1, CheckForUpdates = 2, ClearHistory = 3 }

    /// <summary>Presses a button below the settings, in a settings window that was just opened.</summary>
    public static async Task PressSettingsButton(SettingsButton button)
    {
        for (var i = 0; i < (int)button; i++) { Xdotool("key", "shift+Tab"); await Sleep(120); }
        Xdotool("key", "space");
        await Sleep(500);
    }

    // ------------------------------------------------------------------ what the user sees elsewhere

    /// <summary>Whether the small "Copied" notification is on screen: a window titled "Shapeshifter", lower than the picker.</summary>
    public static bool NotificationVisible() => VisibleWindows("^Shapeshifter$").Any(id => GeometryOf(id).Height < 200);

    /// <summary>Watches for the "Copied" notification for a while; true as soon as it shows.</summary>
    public static async Task<bool> NotificationAppears(int within = 2000)
    {
        for (var until = DateTime.UtcNow.AddMilliseconds(within); DateTime.UtcNow < until; await Sleep(50))
        {
            if (NotificationVisible()) { Screen.KeepSoon(); return true; }
        }
        return false;
    }

    /// <summary>The text on the clipboard.</summary>
    public static string ClipboardText() => Run("xclip", ["-selection", "clipboard", "-o"]);

    static string OpenedFile() => Path.Combine(Root, ".ail", "browser", NotAlphanumeric().Replace(Environment.GetEnvironmentVariable("DISPLAY") ?? "none", "") + ".txt");

    /// <summary>Everything opened in the browser or the file manager so far, oldest first.</summary>
    public static string[] Opened()
    {
        var file = OpenedFile();
        return File.Exists(file) ? File.ReadAllText(file).Split('\n', StringSplitOptions.RemoveEmptyEntries) : [];
    }

    /// <summary>Waits for the next link or folder Shapeshifter opens after <paramref name="before"/> were already open.</summary>
    public static async Task<string?> NextOpened(int before, int timeout = 4000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        for (; ; )
        {
            var all = Opened();
            if (all.Length > before) return all[before];
            if (DateTime.UtcNow > until) return null;
            await Sleep(100);
        }
    }

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NotAlphanumeric();
}

/// <summary>One target app for all the scenarios of a check of the app shell; closes the settings at the end, as bun's afterAll did.</summary>
public sealed class AppFixture : IAsyncLifetime
{
    TargetApp? app;

    public TargetApp App => app ?? throw new InvalidOperationException("the target app has not started");

    public async ValueTask InitializeAsync() => app = await TargetApp.Start();

    public async ValueTask DisposeAsync()
    {
        await AppDriver.CloseSettings();
        await Desktop.ReleaseAll();
        if (app is not null) await app.DisposeAsync();
    }
}
