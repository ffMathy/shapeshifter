// What the search checks need beyond checks/e2e/Desktop.cs, still only what a user sees and does: which window has
// the keyboard focus, typing into it, the words on the picker (read from a screenshot with OCR), and copying from
// other apps (xclip inside a small xterm with a title of our choosing, so the copy comes from a window the user
// could name).
//
// Needs, besides Desktop.cs: xterm, xclip, ImageMagick (import, convert) and tesseract.
using System.Globalization;
using System.Text.RegularExpressions;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public static partial class SearchDriver
{
    // One thread for tesseract: on a busy machine its thread pool makes it many times slower.
    static readonly Dictionary<string, string> OneThread = new() { ["OMP_THREAD_LIMIT"] = "1" };

    static string Exec(string file, params string[] args) => Run(file, args, OneThread);

    /// <summary>The title of the window that has the keyboard focus.</summary>
    public static string FocusedWindowName() => Exec("xdotool", "getactivewindow", "getwindowname").Trim();

    /// <summary>Waits until the window titled <paramref name="name"/> has the keyboard focus.</summary>
    public static async Task<bool> WaitForFocus(string name, int timeout = 3000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        while (FocusedWindowName() != name)
        {
            if (DateTime.UtcNow > until) return false;
            await Sleep(50);
        }
        return true;
    }

    /// <summary>Holds Ctrl and V, taps S and lets go: a search, started before the picker would show on its own.</summary>
    public static async Task StartSearch()
    {
        await KeyDown("ctrl");
        await KeyDown("v");
        await Tap("s");
        await KeyUp("v");
        await KeyUp("ctrl");
        if (!await WaitForFocus("Shapeshifter")) throw new InvalidOperationException("the picker did not take the keyboard focus for the search");
        await Sleep(200);
    }

    /// <summary>Types text with real key presses into whatever has the focus.</summary>
    public static async Task Type(string text)
    {
        Exec("xdotool", "type", "--delay", "40", text);
        await Sleep(300);
    }

    /// <summary>Clicks at a point on the screen.</summary>
    public static async Task ClickAt(int x, int y)
    {
        Exec("xdotool", "mousemove", x.ToString(CultureInfo.InvariantCulture), y.ToString(CultureInfo.InvariantCulture), "click", "1");
        await Sleep(300);
    }

    static readonly string Scratch = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"ail-search-{Environment.ProcessId}")).FullName;

    /// <summary>The words on the picker, read from a screenshot of it. Lower-cased, with runs of white space made single.</summary>
    public static string PickerWords()
    {
        var id = Exec("xdotool", "search", "--onlyvisible", "--name", "^Shapeshifter$").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(w =>
            {
                var m = HeightPattern().Match(Exec("xdotool", "getwindowgeometry", w));
                return m.Success && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) >= 200;
            });
        if (id is null) return "";
        Screen.Keep();
        var shot = Path.Combine(Scratch, "picker.png");
        var big = Path.Combine(Scratch, "picker-big.png");
        Exec("import", "-window", id, shot);
        // Three times: as it is, in black and white, and with the contrast raised, which each catch the pale placeholder
        // in some places on the screen and not in others.
        var words = "";
        foreach (var extra in new[] { Array.Empty<string>(), ["-threshold", "90%"], ["-level", "60%,95%"] })
        {
            Exec("convert", [shot, "-resize", "200%", "-colorspace", "Gray", .. extra, big]);
            words += " " + Exec("tesseract", big, "stdout");
        }
        return Spaces().Replace(words.Replace('“', '"').Replace('”', '"'), " ").ToLowerInvariant();
    }

    /// <summary>Waits until the picker shows all of <paramref name="words"/> (case ignored).</summary>
    public static async Task<string> WaitForPickerWords(string[] words, int timeout = 8000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        var seen = PickerWords();
        while (!words.All(w => seen.Contains(w.ToLowerInvariant(), StringComparison.Ordinal)) && DateTime.UtcNow < until)
        {
            await Sleep(200);
            seen = PickerWords();
        }
        return seen;
    }

    /// <summary>
    /// Copies in another app: an xterm titled <paramref name="title"/> puts <paramref name="data"/> on the clipboard as
    /// <paramref name="type"/> (with xclip, which keeps serving it after the xterm closes), then the clipboard manager is
    /// given time to notice. Plain text is copied with type "text", which offers it the way terminals do (UTF8_STRING,
    /// STRING, TEXT).
    /// </summary>
    public static async Task CopyFromWindow(string title, string type, byte[] data)
    {
        var file = Path.Combine(Scratch, $"copy-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(file, data);
        var target = type == "text" ? "" : $"-t '{type}'";
        Exec("xterm", "-T", title, "-e", "sh", "-c", $"sleep 0.5; xclip -selection clipboard {target} -i '{file}'; sleep 1");
        await Sleep(800);
    }

    public static Task CopyFromWindow(string title, string type, string data) =>
        CopyFromWindow(title, type, System.Text.Encoding.UTF8.GetBytes(data));

    /// <summary>A file on disk, as the user would copy it in a file manager; returns its path.</summary>
    public static string MakeFile(string name, string content = "hello")
    {
        // Not under a folder named "files", so the path never contains the kind word by chance.
        var dir = Directory.CreateDirectory(Path.Combine(Scratch, "docs")).FullName;
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>The kinds of data on the clipboard right now (xclip TARGETS).</summary>
    public static string[] ClipboardTargets() =>
        Exec("xclip", "-o", "-selection", "clipboard", "-t", "TARGETS").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>The clipboard's content of one kind.</summary>
    public static string ClipboardAs(string type) => Exec("xclip", "-o", "-selection", "clipboard", "-t", type);

    /// <summary>A small square of a random colour, as PNG: a picture never copied before.</summary>
    public static byte[] SquarePng()
    {
        var colour = "#" + Random.Shared.Next(0, 0xffffff).ToString("x6", CultureInfo.InvariantCulture);
        return RunBytes("convert", ["-size", "24x24", $"xc:{colour}", "png:-"]).Output;
    }

    [GeneratedRegex(@"Geometry:\s*\d+x(\d+)")]
    private static partial Regex HeightPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
