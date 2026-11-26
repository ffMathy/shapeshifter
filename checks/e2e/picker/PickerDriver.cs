// Helpers for the picker's end-to-end checks: where its window is, what it shows (read back with OCR, as a person
// reads the screen) and which window has the keyboard. It knows nothing of Shapeshifter's code. Some checks need a
// desktop of their own (a short screen, another language): they start the app the way ail.config.json says, on a
// spare display, with an environment variable a user could set, and stop it again.
//
// Needs, besides what Desktop.cs needs: ImageMagick (import, convert), xdpyinfo, xprop, xclip, Xvfb, and tesseract
// (apt-get install tesseract-ocr) for reading the screen.
// Spare displays come from AIL_PICKER_SPARE_DISPLAYS (default ":152,:153,:154").
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Shapeshifter.Checks;

/// <summary>A window's place and size on screen, in pixels.</summary>
public readonly record struct Box(int X, int Y, int Width, int Height);

/// <summary>A word OCR found, and where (in the screenshot's pixels).</summary>
public readonly record struct Word(string Text, int Left, int Top);

public static partial class PickerDriver
{
    static readonly string Shots = Directory.CreateTempSubdirectory("ail-picker-").FullName;

    /// <summary>
    /// The project root: <see cref="Desktop.Root"/>, else (when the checks were built outside the project, and the
    /// test runner works in the build's folder) the folder with ail.config.json above the shell's folder ($PWD).
    /// </summary>
    public static string Root { get; } = File.Exists(Path.Combine(Desktop.Root, "ail.config.json")) ? Desktop.Root : RootAbovePwd();

    static string RootAbovePwd()
    {
        var pwd = Environment.GetEnvironmentVariable("PWD");
        for (var dir = string.IsNullOrEmpty(pwd) ? null : new DirectoryInfo(pwd); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "ail.config.json"))) return dir.FullName;
        return Desktop.Root;
    }

    public static string MainDisplay => Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";

    static string Run(string[] cmd, string? display = null, IDictionary<string, string>? env = null)
    {
        var all = new Dictionary<string, string> { ["DISPLAY"] = display ?? MainDisplay };
        if (env is not null) foreach (var (k, v) in env) all[k] = v;
        return Desktop.Run(cmd[0], cmd[1..], all);
    }

    static string Xdotool(string? display, params string[] args) => Run(["xdotool", .. args], display);

    static int Int(string s) => int.Parse(s, CultureInfo.InvariantCulture);

    public static async Task KeyDown(string key, string? display = null, int after = 60) { Xdotool(display, "keydown", key); await Desktop.Sleep(after); }

    public static async Task KeyUp(string key, string? display = null, int after = 60) { Xdotool(display, "keyup", key); await Desktop.Sleep(after); }

    public static async Task Tap(string key, string? display = null, int after = 60) { await KeyDown(key, display, 30); await KeyUp(key, display, after); }

    public static async Task ReleaseAll(string? display = null)
    {
        foreach (var k in new[] { "v", "space", "shift", "ctrl" }) Xdotool(display, "keyup", k);
        await Desktop.Sleep(100);
    }

    public static void MovePointer(int x, int y, string? display = null) =>
        Xdotool(display, "mousemove", x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture));

    /// <summary>The size of the screen, in pixels.</summary>
    public static (int Width, int Height) ScreenSize(string? display = null)
    {
        var m = DimensionsPattern().Match(Run(["xdpyinfo"], display));
        if (!m.Success) throw new InvalidOperationException($"no screen on {display ?? MainDisplay}");
        return (Int(m.Groups[1].Value), Int(m.Groups[2].Value));
    }

    /// <summary>
    /// The screen's working area: the screen less the panels a desktop keeps there (the window manager's
    /// _NET_WORKAREA), which is what the picker places itself in. The whole screen when the window manager reports none.
    /// </summary>
    public static Box WorkArea(string? display = null)
    {
        var m = WorkAreaPattern().Match(Run(["xprop", "-root", "_NET_WORKAREA"], display));
        if (m.Success) return new(Int(m.Groups[1].Value), Int(m.Groups[2].Value), Int(m.Groups[3].Value), Int(m.Groups[4].Value));
        var (width, height) = ScreenSize(display);
        return new(0, 0, width, height);
    }

    /// <summary>The picker on screen: the visible window titled "Shapeshifter" taller than the copy notification; null when none.</summary>
    public static Box? Picker(string? display = null)
    {
        foreach (var id in Xdotool(display, "search", "--onlyvisible", "--name", "^Shapeshifter$").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var g = Xdotool(display, "getwindowgeometry", id);
            var p = PositionPattern().Match(g);
            var s = GeometryPattern().Match(g);
            if (p.Success && s.Success && Int(s.Groups[2].Value) >= 200)
                return new(Int(p.Groups[1].Value), Int(p.Groups[2].Value), Int(s.Groups[1].Value), Int(s.Groups[2].Value));
        }
        return null;
    }

    public static async Task<bool> WaitForPicker(bool visible, string? display = null, int timeout = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while (Picker(display).HasValue != visible)
        {
            if (DateTime.UtcNow > until) return !visible;
            await Desktop.Sleep(50);
        }
        return visible;
    }

    /// <summary>Waits until the picker's size and place stop changing (it sizes itself to its content).</summary>
    public static async Task<Box> SettledPicker(string? display = null)
    {
        Box? last = null;
        for (var i = 0; i < 40; i++)
        {
            var box = Picker(display);
            if (box is not null && box == last) return box.Value;
            last = box;
            await Desktop.Sleep(150);
        }
        throw new InvalidOperationException("the picker did not settle on screen");
    }

    /// <summary>Ctrl held, V held until the picker shows, then V let go: the picker stays up while Ctrl is held.</summary>
    public static async Task<Box> HoldThePickerOpen(string? display = null)
    {
        await KeyDown("ctrl", display);
        await KeyDown("v", display, 0);
        var shown = await WaitForPicker(true, display);
        await KeyUp("v", display);
        if (!shown) throw new InvalidOperationException("the picker did not appear while Ctrl+V was held");
        var box = await SettledPicker(display);
        Screen.Keep(display ?? MainDisplay);
        return box;
    }

    /// <summary>Escape, then Ctrl let go: the picker closes without pasting.</summary>
    public static async Task CancelThePicker(string? display = null)
    {
        await Tap("Escape", display);
        await KeyUp("ctrl", display);
        await WaitForPicker(false, display);
    }

    /// <summary>The name of the window that has the keyboard focus.</summary>
    public static string FocusedWindowName(string? display = null) => Xdotool(display, "getactivewindow", "getwindowname").Trim();

    /// <summary>Puts text (or, with a type such as image/png, a file's bytes) on the clipboard, as another application would.</summary>
    public static async Task CopyWithXclip(string data, string? type = null, string? display = null)
    {
        var image = type?.StartsWith("image/", StringComparison.Ordinal) == true;
        var info = new ProcessStartInfo("xclip")
        {
            UseShellExecute = false,
            RedirectStandardInput = !image,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in new[] { "-selection", "clipboard" }) info.ArgumentList.Add(a);
        if (type is not null) { info.ArgumentList.Add("-t"); info.ArgumentList.Add(type); }
        info.ArgumentList.Add("-i");
        if (image) info.ArgumentList.Add(data);
        info.Environment["DISPLAY"] = display ?? MainDisplay;
        // xclip stays behind to serve the clipboard, as the app that copied would; it is not waited for.
        var proc = Process.Start(info)!;
        proc.OutputDataReceived += (_, _) => { };
        proc.ErrorDataReceived += (_, _) => { };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        if (!image) { await proc.StandardInput.WriteAsync(data); proc.StandardInput.Close(); }
        await Desktop.Sleep(1000);
    }

    /// <summary>A PNG of the given size in a colour of its own, to copy as an image.</summary>
    public static string MakeImage(int width, int height)
    {
        var file = Path.Combine(Shots, $"copied-{Desktop.Unique()}.png");
        var colour = "#" + Random.Shared.Next(0x1000000).ToString("x6", CultureInfo.InvariantCulture);
        Desktop.Run("convert", ["-size", $"{width}x{height}", $"xc:{colour}", file]);
        return file;
    }

    /// <summary>A screenshot of the picker, enlarged twice so the small type reads well.</summary>
    public static string ShootPicker(Box box, string? display = null)
    {
        var file = Path.Combine(Shots, $"picker-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{Desktop.Unique()}.png");
        Run(["import", "-window", "root", "-crop", $"{box.Width}x{box.Height}+{box.X}+{box.Y}", "+repage", "-resize", "200%", file], display);
        return file;
    }

    static readonly Dictionary<string, string> OneThread = new() { ["OMP_THREAD_LIMIT"] = "1" };

    /// <summary>What a screenshot says, read with OCR (English letters).</summary>
    public static string ReadText(string png) => Desktop.Run("tesseract", [png, "-", "--psm", "3"], OneThread);

    /// <summary>Every word OCR finds, with where it is (in the screenshot's pixels).</summary>
    public static List<Word> ReadWords(string png) =>
        [.. Desktop.Run("tesseract", [png, "-", "--psm", "11", "tsv"], OneThread).Split('\n').Skip(1)
            .Select(line => line.Split('\t'))
            .Where(c => c.Length >= 12 && c[11].Trim().Length > 0)
            .Select(c => new Word(c[11].Trim(), Int(c[6]), Int(c[7])))];

    /// <summary>Collapses OCR's spacing, so a check can look for a phrase.</summary>
    public static string Squash(string text) => Whitespace().Replace(text, " ").Trim();

    [GeneratedRegex(@"dimensions:\s*(\d+)x(\d+)")]
    private static partial Regex DimensionsPattern();

    [GeneratedRegex(@"=\s*(\d+),\s*(\d+),\s*(\d+),\s*(\d+)")]
    private static partial Regex WorkAreaPattern();

    [GeneratedRegex(@"Position:\s*(-?\d+),(-?\d+)")]
    private static partial Regex PositionPattern();

    [GeneratedRegex(@"Geometry:\s*(\d+)x(\d+)")]
    private static partial Regex GeometryPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

/// <summary>
/// Shapeshifter on a desktop of its own: a fresh Xvfb screen of the given size on a spare display, the app started as
/// ail.config.json says (run.app, else run.legacy) with a fresh state folder and the extra environment variables.
/// </summary>
public sealed class OwnDesktop
{
    readonly Process xvfb;
    readonly Process app;

    public string Display { get; }

    OwnDesktop(string display, Process xvfb, Process app) { Display = display; this.xvfb = xvfb; this.app = app; }

    public static async Task<OwnDesktop> Start(int width, int height, string name, IDictionary<string, string>? env = null)
    {
        var display = SpareDisplay();
        var xvfb = Quiet(new ProcessStartInfo("Xvfb") { ArgumentList = { display, "-screen", "0", $"{width}x{height}x24", "-nolisten", "tcp" } });
        for (var i = 0; i < 50 && !File.Exists($"/tmp/.X11-unix/X{display[1..]}"); i++) await Desktop.Sleep(100);
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(PickerDriver.Root, "ail.config.json")));
        var run = config.RootElement.GetProperty("run");
        var how = run.TryGetProperty("app", out var a) ? a : run.GetProperty("legacy");
        var info = new ProcessStartInfo("bash")
        {
            ArgumentList = { "-c", how.GetProperty("start").GetString()! },
            WorkingDirectory = Path.GetFullPath(Path.Combine(PickerDriver.Root, how.TryGetProperty("cwd", out var cwd) ? cwd.GetString()! : ".")),
        };
        if (how.TryGetProperty("env", out var howEnv))
            foreach (var p in howEnv.EnumerateObject()) info.Environment[p.Name] = p.Value.GetString();
        info.Environment["DISPLAY"] = display;
        info.Environment["AIL_DISPLAY"] = display;
        info.Environment["AIL_STATE"] = $".ail/picker-{name}";
        info.Environment["AIL_SKIP_BUILD"] = "1";
        if (env is not null) foreach (var (k, v) in env) info.Environment[k] = v;
        var desktop = new OwnDesktop(display, xvfb, Quiet(info));
        try { await desktop.WaitUntilReady(); }
        catch { await desktop.Stop(); throw; }
        return desktop;
    }

    static Process Quiet(ProcessStartInfo info)
    {
        info.UseShellExecute = false;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        var process = Process.Start(info)!;
        process.OutputDataReceived += (_, _) => { };
        process.ErrorDataReceived += (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    /// <summary>Ready once holding Ctrl+V shows the picker, as a person would find out.</summary>
    async Task WaitUntilReady(int timeout = 120_000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < until)
        {
            await Desktop.Sleep(1500);
            await PickerDriver.KeyDown("ctrl", Display);
            await PickerDriver.KeyDown("v", Display, 0);
            var shown = await PickerDriver.WaitForPicker(true, Display, 2000);
            await PickerDriver.KeyUp("v", Display);
            if (shown) { await PickerDriver.CancelThePicker(Display); return; }
            await PickerDriver.KeyUp("ctrl", Display);
        }
        throw new TimeoutException($"Shapeshifter did not start on {Display}");
    }

    public async Task Stop()
    {
        Screen.KeepIfNone(Display);
        // SIGTERM to the start script, which stops the app, the window manager and the tray it started.
        Desktop.Run("kill", ["-TERM", app.Id.ToString(CultureInfo.InvariantCulture)]);
        using (var cts = new CancellationTokenSource(5000))
        {
            try { await app.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { /* waited long enough */ }
        }
        // SIGTERM, so Xvfb removes its lock and socket and the display is free for the next one.
        Desktop.Run("kill", ["-TERM", xvfb.Id.ToString(CultureInfo.InvariantCulture)]);
        await xvfb.WaitForExitAsync();
        app.Dispose();
        xvfb.Dispose();
    }

    static string SpareDisplay()
    {
        var spares = (Environment.GetEnvironmentVariable("AIL_PICKER_SPARE_DISPLAYS") ?? ":152,:153,:154").Split(',').Select(d => d.Trim()).ToArray();
        var free = spares.FirstOrDefault(d => !File.Exists($"/tmp/.X11-unix/X{d[1..]}") && !File.Exists($"/tmp/.X{d[1..]}-lock"));
        return free ?? throw new InvalidOperationException($"no free spare display among {string.Join(", ", spares)}");
    }
}
