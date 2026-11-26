// Helpers for the end-to-end checks of the actions, deleting, pinning and the remembered history. Like Desktop.cs
// they see only what a user sees: the clipboard (xclip), the picker on screen (read with OCR, tesseract), files on
// disk and windows. Nothing here reads Shapeshifter's code, logs or state file.
//
// The restart checks need an app they may quit and start again, which the shared one is not, so PrivateDesktop
// starts a desktop of its own: an X server, openbox, a panel with a tray (to quit Shapeshifter and open its settings
// as a user does), and Shapeshifter with its own SHAPESHIFTER_HOME.
//
// Needs: xclip, xdotool, xwininfo, ImageMagick (import, convert, identify), tesseract, Xvfb, openbox, dbus-daemon,
// xfce4-panel, the .NET runtime. AIL_SHAPESHIFTER_DLL overrides the app it starts (default: the Release build).
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public static partial class ActionsDriver
{
    // ---------- the clipboard ----------

    /// <summary>The formats the clipboard offers right now.</summary>
    public static string[] ClipboardFormats() =>
        [.. Run("xclip", ["-selection", "clipboard", "-t", "TARGETS", "-o"]).Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0)];

    /// <summary>The clipboard's content in one format.</summary>
    public static string ClipboardAs(string format) => Run("xclip", ["-selection", "clipboard", "-t", format, "-o"]);

    /// <summary>Copies data of one format, as an app that owns the clipboard does (xclip keeps serving it), then gives Shapeshifter time to notice.</summary>
    public static async Task CopyAs(string format, string data) => await CopyAs(format, System.Text.Encoding.UTF8.GetBytes(data));

    /// <inheritdoc cref="CopyAs(string, string)"/>
    public static async Task CopyAs(string format, byte[] data)
    {
        // xclip forks to keep serving the clipboard, and its child keeps any pipe it was given: send its output nowhere.
        var info = new ProcessStartInfo("sh") { RedirectStandardInput = true, UseShellExecute = false };
        foreach (var a in new[] { "-c", "exec xclip -selection clipboard -t \"$0\" -i >/dev/null 2>&1", format }) info.ArgumentList.Add(a);
        using (var p = Process.Start(info)!)
        {
            await p.StandardInput.BaseStream.WriteAsync(data);
            p.StandardInput.Close();
        }
        await Sleep(1200);
    }

    /// <summary>A small solid PNG image of the given size, made with ImageMagick.</summary>
    public static byte[] MakePng(int width, int height, string colour = "red") =>
        RunBytes("convert", ["-size", $"{width}x{height}", $"xc:{colour}", "png:-"]).Output;

    /// <summary>The local paths of the files on the clipboard (text/uri-list).</summary>
    public static string[] ClipboardFiles() =>
        [.. LineBreak().Split(ClipboardAs("text/uri-list")).Select(s => s.Trim()).Where(s => s.StartsWith("file://", StringComparison.Ordinal))
            .Select(s => Uri.UnescapeDataString(new Uri(s).AbsolutePath))];

    // ---------- reading the picker ----------

    /// <summary>A short id that OCR reads back reliably (no 0/o, 1/l/i, 5/s look-alikes), for texts a check looks for on screen.</summary>
    public static string ReadableId() => new(Random.Shared.GetItems("acdefhkmnprtwxyz".AsSpan(), 6));

    /// <summary>Tesseract, one thread (several checks may read the screen at once).</summary>
    static string Tesseract(params string[] args) => Run("tesseract", args, new Dictionary<string, string> { ["OMP_THREAD_LIMIT"] = "1" });

    static string? PickerWindow() =>
        Xdotool("search", "--onlyvisible", "--name", "^Shapeshifter$").Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(id =>
        {
            var m = Height().Match(Xdotool("getwindowgeometry", id));
            return m.Success && int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) >= 200;
        });

    /// <summary>Ways to prepare a screenshot for OCR; each misreads different things (the chosen row's colour and ring, small grey text).</summary>
    static readonly string[][] OcrPasses =
    [
        ["-resize", "300%", "-colorspace", "Gray"],
        ["-resize", "200%", "-colorspace", "Gray"],
        ["-colorspace", "Gray", "-resize", "300%", "-sharpen", "0x1"],
    ];

    /// <summary>OCR of a part of the picker, one text per pass: "actions" is the right-hand column, "history" the left one, "all" the whole picker.</summary>
    public static string[] ReadPickerPasses(string part = "all")
    {
        var id = PickerWindow() ?? throw new InvalidOperationException("the picker is not on screen");
        Screen.Keep();
        var dir = Directory.CreateTempSubdirectory("ail-actions-ocr-").FullName;
        var shot = Path.Combine(dir, "picker.png");
        Run("import", ["-window", id, shot]);
        var size = Run("identify", ["-format", "%w %h", shot]).Split(' ').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var (w, h) = (size[0], size[1]);
        // The actions column is 284 px wide plus the margins at the right; the history takes the rest.
        var crop = part switch
        {
            "actions" => $"310x{h}+{w - 310}+0",
            "history" => $"{w - 310}x{h}+0+0",
            _ => $"{w}x{h}+0+0",
        };
        return [.. OcrPasses.Select((pass, i) =>
        {
            var big = Path.Combine(dir, $"pass{i}.png");
            Run("convert", [shot, "-crop", crop, "+repage", .. pass, big]);
            return Tesseract(big, "-", "--psm", "6");
        })];
    }

    /// <summary>The first OCR pass in which every one of <paramref name="needles"/> is found, else every pass joined (so "not found" means no pass found it).</summary>
    public static string ReadPicker(string part = "all", params string[] needles)
    {
        var passes = ReadPickerPasses(part);
        return passes.FirstOrDefault(t => needles.Length > 0 && needles.All(n => t.Contains(n, StringComparison.Ordinal))) ?? string.Join("\n", passes);
    }

    /// <summary>Every action title, in the order they are shown, as read from the screen.</summary>
    public static readonly string[] ActionTitles = ["Paste", "Paste as file", "Paste as plain text", "Paste trimmed", "Upload and paste link", "Paste as compressed file",
        "Copy image from link", "Open link", "Open links", "Pin to clipboard", "Unpin from clipboard"];

    public static string[] ActionTitlesOnScreen()
    {
        // A title shares its line only with what OCR makes of the icon before it and the tick after it: a few stray marks.
        static bool IsStray(string rest)
        {
            var marks = rest.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return marks.Length <= 2 && marks.All(m => m.Length <= 2);
        }
        static string ReplaceFirst(string s, string what)
        {
            var at = s.IndexOf(what, StringComparison.Ordinal);
            return at < 0 ? s : string.Concat(s.AsSpan(0, at), " ", s.AsSpan(at + what.Length));
        }
        // Each pass may miss a title; none invents one, so the longest list is the most complete.
        var lists = ReadPickerPasses("actions").Select(ocr =>
        {
            var titles = new List<string>();
            foreach (var raw in ocr.Split('\n'))
            {
                var line = Spaces().Replace(raw, " ").Trim();
                var found = ActionTitles
                    .Where(t => Regex.IsMatch(line, $@"(^|\s){Regex.Escape(t)}(\s|$)") && IsStray(ReplaceFirst(line, t)))
                    .OrderByDescending(t => t.Length).FirstOrDefault();
                if (found is not null) titles.Add(found);
            }
            return titles;
        });
        return [.. lists.Aggregate((a, b) => b.Count > a.Count ? b : a)];
    }

    /// <summary>The "N items" count in the picker's header.</summary>
    public static async Task<int> ItemCountOnScreen()
    {
        // The picker may still be settling into its new size; read again a moment later.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var m = ReadPickerPasses("history").Select(t => ItemCount().Match(t)).FirstOrDefault(m => m.Success);
            if (m is not null) return int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            await Sleep(400);
        }
        throw new InvalidOperationException("no item count on screen");
    }

    // ---------- gestures ----------

    /// <summary>Holds Ctrl+V until the picker is up, then moves <paramref name="down"/> items down; Ctrl stays held.</summary>
    public static async Task OpenPicker(int down = 0)
    {
        await KeyDown("ctrl");
        await HoldVUntilThePickerAppears();
        for (var i = 0; i < down; i++) await Tap("Down");
        await Sleep(250);
    }

    /// <summary>With the picker up: Right to the actions, <paramref name="down"/> actions down, then lets go of Ctrl.</summary>
    public static async Task ChooseAction(int down)
    {
        await Tap("Right");
        for (var i = 0; i < down; i++) await Tap("Down");
        await Sleep(200);
        await KeyUp("ctrl");
        await Sleep(1200);
    }

    [GeneratedRegex(@"\r?\n")] private static partial Regex LineBreak();
    [GeneratedRegex(@"\s+")] private static partial Regex Spaces();
    [GeneratedRegex(@"(\d+)\s+items?\b")] private static partial Regex ItemCount();
    [GeneratedRegex(@"Geometry:\s*\d+x(\d+)")] private static partial Regex Height();
}

/// <summary>A desktop of its own, for the checks that quit and restart Shapeshifter.</summary>
public sealed partial class PrivateDesktop
{
    public string Display { get; }

    /// <summary>Where this Shapeshifter keeps everything (its SHAPESHIFTER_HOME); data/ holds what it saves.</summary>
    public string Home { get; }

    readonly string dir;
    readonly List<Process> procs = [];
    Process? app;
    readonly Dictionary<string, string> env;

    PrivateDesktop(int display, string dir)
    {
        Display = $":{display}";
        this.dir = dir;
        Home = Path.Combine(dir, "home");
        Directory.CreateDirectory(Home);
        env = new() { ["DISPLAY"] = Display };
    }

    static int FreeDisplay()
    {
        for (var n = 171; n < 199; n++)
            if (!File.Exists($"/tmp/.X11-unix/X{n}") && !File.Exists($"/tmp/.X{n}-lock")) return n;
        throw new InvalidOperationException("no free X display between :171 and :198");
    }

    Process Spawn(string file, IEnumerable<string> args, IDictionary<string, string>? with = null)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) info.ArgumentList.Add(a);
        foreach (var (k, v) in with ?? env) info.Environment[k] = v;
        var p = Process.Start(info)!;
        p.OutputDataReceived += (_, _) => { };
        p.ErrorDataReceived += (_, _) => { };
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        procs.Add(p);
        return p;
    }

    string RunHere(string file, params string[] args) => Run(file, args, env);

    public static async Task<PrivateDesktop> Start()
    {
        var desk = new PrivateDesktop(FreeDisplay(), Directory.CreateTempSubdirectory("ail-actions-").FullName);
        desk.Spawn("Xvfb", [desk.Display, "-screen", "0", "1280x800x24", "-nolisten", "tcp"]);
        for (var i = 0; i < 50 && RunBytes("xdpyinfo", [], desk.env).Exit != 0; i++) await Sleep(100);
        desk.Spawn("openbox", []);
        await Sleep(800);
        // A session bus and a panel with a tray: Avalonia shows its tray icon over D-Bus (StatusNotifierItem).
        var tray = Path.Combine(desk.dir, "tray");
        var conf = Path.Combine(tray, "config/xfce4/xfconf/xfce-perchannel-xml");
        Directory.CreateDirectory(conf);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(Path.Combine(tray, "runtime"));
        else Directory.CreateDirectory(Path.Combine(tray, "runtime"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await File.WriteAllTextAsync(Path.Combine(conf, "xfce4-panel.xml"), Panel);
        var bus = $"unix:path={Path.Combine(tray, "runtime/bus")}";
        var trayEnv = new Dictionary<string, string>(desk.env)
        {
            ["HOME"] = tray,
            ["XDG_CONFIG_HOME"] = Path.Combine(tray, "config"),
            ["XDG_DATA_HOME"] = Path.Combine(tray, "data"),
            ["XDG_CACHE_HOME"] = Path.Combine(tray, "cache"),
            ["XDG_RUNTIME_DIR"] = Path.Combine(tray, "runtime"),
        };
        desk.Spawn("dbus-daemon", ["--session", "--nofork", $"--address={bus}"], trayEnv);
        for (var i = 0; i < 50 && !File.Exists(Path.Combine(tray, "runtime/bus")); i++) await Sleep(100);
        desk.env["DBUS_SESSION_BUS_ADDRESS"] = bus;
        trayEnv["DBUS_SESSION_BUS_ADDRESS"] = bus;
        desk.Spawn("xfce4-panel", ["--disable-wm-check", $"--sm-client-id=ail-actions-{desk.Display[1..]}"], trayEnv);
        await Sleep(2500);
        return desk;
    }

    /// <summary>Starts Shapeshifter and waits until it is ready, as <c>run.legacy</c> does.</summary>
    public async Task StartApp()
    {
        var dll = Path.GetFullPath(Path.Combine(Root, Environment.GetEnvironmentVariable("AIL_SHAPESHIFTER_DLL") ?? "src/Shapeshifter.Desktop/bin/Release/net11.0/Shapeshifter.dll"));
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet");
        var info = new ProcessStartInfo(File.Exists(local) ? local : "dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Root,
        };
        info.ArgumentList.Add(dll);
        info.ArgumentList.Add("--no-install");
        foreach (var (k, v) in env) info.Environment[k] = v;
        info.Environment["SHAPESHIFTER_HOME"] = Home;
        info.Environment["SHAPESHIFTER_NO_UPDATE"] = "1";
        info.Environment["SHAPESHIFTER_NO_INSTALL"] = "1";
        info.Environment["SHAPESHIFTER_LOOK"] = "linux";
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = Process.Start(info)!;
        app = started;
        // Keep reading what it writes (and drop it), so it never blocks on a full pipe.
        started.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null) ready.TrySetException(new InvalidOperationException("Shapeshifter exited while starting"));
            else if (e.Data.Contains("SHAPESHIFTER_READY", StringComparison.Ordinal)) ready.TrySetResult();
        };
        started.ErrorDataReceived += (_, _) => { };
        started.BeginOutputReadLine();
        started.BeginErrorReadLine();
        try { await ready.Task.WaitAsync(TimeSpan.FromSeconds(90)); }
        catch (TimeoutException) { throw new TimeoutException("Shapeshifter did not get ready"); }
        await Sleep(1500);
    }

    static (int X, int Y) TrayIcon() => (1280 - 17, 800 - 16);

    /// <summary>Quits through the tray menu's last item, "Quit Shapeshifter", and waits for the app to be gone.</summary>
    public async Task QuitApp()
    {
        var (x, y) = TrayIcon();
        RunHere("xdotool", "mousemove", $"{x}", $"{y}", "click", "3");
        await Sleep(1200);
        RunHere("xdotool", "key", "Up");
        await Sleep(300);
        RunHere("xdotool", "key", "Return");
        using var cts = new CancellationTokenSource(15_000);
        try { await app!.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException) { throw new TimeoutException("Shapeshifter did not quit from its tray menu"); }
        app.Dispose();
        app = null;
    }

    /// <summary>Opens the settings with a click on the tray icon and flips the "Remember history after restart" switch.</summary>
    public async Task ToggleRememberHistory()
    {
        var (x, y) = TrayIcon();
        RunHere("xdotool", "mousemove", $"{x}", $"{y}", "click", "1");
        var id = "";
        for (var i = 0; i < 50 && id.Length == 0; i++)
        {
            await Sleep(100);
            id = RunHere("xdotool", "search", "--onlyvisible", "--name", "^Shapeshifter Settings$").Split('\n')[0].Trim();
        }
        if (id.Length == 0) throw new InvalidOperationException("the settings window did not open");
        await Sleep(800);
        var info = RunHere("xwininfo", "-id", id);
        static int Num(Regex r, string s) => int.Parse(r.Match(s).Groups[1].Value, CultureInfo.InvariantCulture);
        var left = Num(Left(), info);
        var top = Num(Top(), info);
        var width = Num(Width(), info);
        // Find the row by its title on screen; its switch sits at the right end of the row.
        var shot = Path.Combine(dir, "settings.png");
        RunHere("import", "-window", id, shot);
        var tsv = Run("tesseract", [shot, "-", "tsv"], new Dictionary<string, string> { ["OMP_THREAD_LIMIT"] = "1" });
        var word = tsv.Split('\n').Select(l => l.Split('\t')).FirstOrDefault(c => c.Length > 11 && c[11].Trim() == "Remember")
            ?? throw new InvalidOperationException("no \"Remember history after restart\" in the settings");
        var rowY = top + double.Parse(word[7], CultureInfo.InvariantCulture) + double.Parse(word[9], CultureInfo.InvariantCulture) / 2 + 10;
        RunHere("xdotool", "mousemove", $"{left + width - 66}", $"{(int)Math.Round(rowY, MidpointRounding.AwayFromZero)}", "click", "1");
        await Sleep(1500);
        RunHere("xdotool", "windowactivate", "--sync", id, "key", "alt+F4");
        await Sleep(800);
    }

    /// <summary>Points the shared helpers (Desktop, TargetApp, OCR, screenshots) at this desktop; returns an action that points them back.</summary>
    public Action Use()
    {
        var before = Environment.GetEnvironmentVariable("DISPLAY");
        Environment.SetEnvironmentVariable("DISPLAY", Display);
        return () => Environment.SetEnvironmentVariable("DISPLAY", before);
    }

    public async Task Stop()
    {
        Screen.KeepIfNone(Display);
        if (app is not null) Terminate(app);
        for (var i = procs.Count - 1; i >= 0; i--) Terminate(procs[i]);
        await Sleep(300);
        foreach (var p in procs)
        {
            // Stragglers are killed outright, so no X server or panel outlives its scenario.
            try { if (!p.WaitForExit(3000)) p.Kill(true); } catch (InvalidOperationException) { /* already gone */ }
            p.Dispose();
        }
        if (app is not null)
        {
            try { if (!app.WaitForExit(3000)) app.Kill(true); } catch (InvalidOperationException) { /* already gone */ }
            app.Dispose();
            app = null;
        }
        procs.Clear();
    }

    /// <summary>SIGTERM, as a user's session ending would send: Xvfb then removes its lock and socket.</summary>
    static void Terminate(Process p)
    {
        try { if (!p.HasExited) Run("kill", ["-TERM", p.Id.ToString(CultureInfo.InvariantCulture)]); }
        catch (InvalidOperationException) { /* already gone */ }
    }

    [GeneratedRegex(@"Absolute upper-left X:\s*(-?\d+)")] private static partial Regex Left();
    [GeneratedRegex(@"Absolute upper-left Y:\s*(-?\d+)")] private static partial Regex Top();
    [GeneratedRegex(@"Width:\s*(\d+)")] private static partial Regex Width();

    const string Panel = """
        <?xml version="1.0" encoding="UTF-8"?>
        <channel name="xfce4-panel" version="1.0">
          <property name="configver" type="int" value="2"/>
          <property name="panels" type="array">
            <value type="int" value="1"/>
            <property name="panel-1" type="empty">
              <property name="position" type="string" value="p=8;x=0;y=0"/>
              <property name="length" type="uint" value="100"/>
              <property name="position-locked" type="bool" value="true"/>
              <property name="size" type="uint" value="32"/>
              <property name="icon-size" type="uint" value="24"/>
              <property name="plugin-ids" type="array"><value type="int" value="1"/><value type="int" value="2"/></property>
            </property>
          </property>
          <property name="plugins" type="empty">
            <property name="plugin-1" type="string" value="separator"><property name="expand" type="bool" value="true"/><property name="style" type="uint" value="0"/></property>
            <property name="plugin-2" type="string" value="systray"><property name="square-icons" type="bool" value="true"/></property>
          </property>
        </channel>

        """;
}

/// <summary>
/// Scenarios that each run on a desktop of their own (<see cref="PrivateDesktop"/>), with their own Shapeshifter and
/// target app; the desktop is closed after each one, and the shared helpers point back at the shared desktop.
/// </summary>
public abstract class PrivateDesktopScenario : Scenario
{
    protected PrivateDesktop? Desk { get; private set; }
    protected TargetApp? App { get; set; }
    Action? restore;

    protected async Task<TargetApp> SetUp()
    {
        Desk = await PrivateDesktop.Start();
        restore = Desk.Use();
        await Desk.StartApp();
        App = await TargetApp.Start();
        return App;
    }

    protected override async Task AfterEach()
    {
        try
        {
            await Desktop.ReleaseAll();
            if (App is not null) await App.DisposeAsync();
            if (Desk is not null) await Desk.Stop();
        }
        finally
        {
            restore?.Invoke();
            App = null;
            Desk = null;
            restore = null;
        }
    }
}
