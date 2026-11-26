// Drives an X11 desktop the way a person would, for the end-to-end checks. It knows nothing of Shapeshifter's
// code: it copies text in another app, presses real keys (xdotool, through XTEST) and reads what arrived in that
// app's text box, and whether a window titled "Shapeshifter" as big as the picker is on screen. So it works against
// any build of the app.
//
// Needs: DISPLAY, xdotool, ImageMagick's import, the .NET runtime, and AIL_TARGET_APP, the path (relative to the
// project root) of a small app with one text box that takes commands on stdin (tests/Shapeshifter.EndToEnd.TargetApp).
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Shapeshifter.Checks;

/// <summary>A window's place and size on screen, in pixels.</summary>
public sealed record Box(int X, int Y, int Width, int Height);

public static partial class Desktop
{
    /// <summary>The project root: AIL_ROOT, else the folder with ail.config.json above the working folder or the checks' build.</summary>
    public static string Root { get; } = FindRoot();

    static string FindRoot()
    {
        if (Environment.GetEnvironmentVariable("AIL_ROOT") is { Length: > 0 } given) return Path.GetFullPath(given);
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "ail.config.json"))) return dir.FullName;
        }
        return Environment.CurrentDirectory;
    }

    /// <summary>The display the checks play on.</summary>
    public static string Display => Environment.GetEnvironmentVariable("DISPLAY") ?? "";

    public static Task Sleep(int ms) => Task.Delay(ms);

    /// <summary>A short id, so texts from different tests never collide in the history.</summary>
    public static string Unique() => new(Random.Shared.GetItems("abcdefghijklmnopqrstuvwxyz0123456789".AsSpan(), 6));

    /// <summary>Runs a program to the end and returns what it wrote to stdout (an empty string if it can't start).</summary>
    public static string Run(string file, IEnumerable<string> args, IDictionary<string, string>? env = null, string? input = null) =>
        Encoding.UTF8.GetString(RunBytes(file, args, env, input).Output);

    /// <summary>Runs a program to the end and returns its exit code and stdout as bytes.</summary>
    public static (int Exit, byte[] Output) RunBytes(string file, IEnumerable<string> args, IDictionary<string, string>? env = null, string? input = null)
    {
        var info = new ProcessStartInfo(file)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = input is not null,
            UseShellExecute = false,
        };
        foreach (var a in args) info.ArgumentList.Add(a);
        if (env is not null) foreach (var (k, v) in env) info.Environment[k] = v;
        Process process;
        try { process = Process.Start(info)!; }
        catch (System.ComponentModel.Win32Exception) { return (-1, []); }
        using (process)
        {
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            if (input is not null) { process.StandardInput.Write(input); process.StandardInput.Close(); }
            using var buffer = new MemoryStream();
            process.StandardOutput.BaseStream.CopyTo(buffer);
            process.WaitForExit();
            return (process.ExitCode, buffer.ToArray());
        }
    }

    public static string Xdotool(params string[] args) => Run("xdotool", args);

    /// <summary>xdotool key names: ctrl, shift, v, Up, Down, Escape, space, Return, 1–9.</summary>
    public static async Task KeyDown(string key, int after = 60) { Xdotool("keydown", key); await Sleep(after); }

    public static async Task KeyUp(string key, int after = 60) { Xdotool("keyup", key); await Sleep(after); }

    public static async Task Tap(string key, int after = 60) { await KeyDown(key, 30); await KeyUp(key, after); }

    /// <summary>Lets go of every key a check may have left down, so one failure does not spoil the next check.</summary>
    public static async Task ReleaseAll()
    {
        foreach (var k in new[] { "v", "space", "shift", "ctrl" }) Xdotool("keyup", k);
        await Sleep(100);
    }

    /// <summary>Whether the picker is on screen: a visible window titled "Shapeshifter", taller than the small "Copied" notification.</summary>
    public static bool PickerVisible() =>
        Xdotool("search", "--onlyvisible", "--name", "^Shapeshifter$").Split('\n', StringSplitOptions.RemoveEmptyEntries).Any(id =>
        {
            var m = HeightPattern().Match(Xdotool("getwindowgeometry", id));
            return m.Success && int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 200;
        });

    /// <summary>Waits until the picker is (or is not) on screen, and returns whether it is.</summary>
    public static async Task<bool> WaitForPicker(bool visible, int timeout = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while (PickerVisible() != visible)
        {
            if (DateTime.UtcNow > until) return !visible;
            await Sleep(50);
        }
        return visible;
    }

    /// <summary>With Ctrl held: holds V until the picker appears, then lets go of V.</summary>
    public static async Task HoldVUntilThePickerAppears()
    {
        await KeyDown("v", 0);
        var shown = await WaitForPicker(true);
        await KeyUp("v");
        if (!shown) throw new InvalidOperationException("the picker did not appear while V was held");
    }

    [GeneratedRegex(@"Geometry:\s*\d+x(\d+)")]
    private static partial Regex HeightPattern();
}
