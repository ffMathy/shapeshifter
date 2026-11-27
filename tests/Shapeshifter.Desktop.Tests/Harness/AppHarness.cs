using System.Globalization;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using Serilog.Core;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Coaching;
using Shapeshifter.Core.Engine;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Persistence;
using Shapeshifter.Core.Platform;
using Shapeshifter.Core.Settings;
using Shapeshifter.Desktop.Services;
using Shapeshifter.Desktop.ViewModels;
using Shapeshifter.Desktop.Views;
using Shapeshifter.Core.Input;

namespace Shapeshifter.Desktop.Tests;

/// <summary>How the app under test is set up: the look, the theme, the display language and the paste shortcut.</summary>
public sealed record AppOptions
{
    /// <summary>The visual identity: Linux (Boxed Sheet), Windows (Mica Sheets) or MacOS (Refraction).</summary>
    public Look Look { get; init; } = Look.Linux;

    /// <summary>Dark instead of light; only the Windows look has a dark variant.</summary>
    public bool Dark { get; init; }

    /// <summary>The display language, as a culture name such as "de"; English when null.</summary>
    public string? Language { get; init; }

    /// <summary>The gesture's modifier as the picker names it: "Ctrl" (default), "Cmd" or "Win".</summary>
    public string Modifier { get; init; } = "Ctrl";

    /// <summary>Whether this copy came from an app store (the Microsoft Store), which updates it: its settings leave updating out.</summary>
    public bool StoreCopy { get; init; }
}

/// <summary>
/// The real Shapeshifter on Avalonia's headless platform, rendered with Skia: the real engine, history, actions, picker,
/// copy notification and settings window, with fakes only for what touches the operating system (the clipboard, the
/// global keyboard hook, the simulated paste, the application in front, file icons and the launcher). A check drives it
/// as a user would: copy things, hold Ctrl+V, tap keys, let go. <see cref="SaveScreen"/> saves what a window shows.
/// </summary>
/// <remarks>
/// Start it with <see cref="RunAsync"/>, which runs the test on the UI thread; await everything it offers there.
/// Time is fake for the engine (<see cref="Advance"/>), so the picker appears only when a check holds the keys long
/// enough, as <see cref="HoldCtrlVAsync"/> does.
/// </remarks>
public sealed class AppHarness : IAsyncDisposable
{
    /// <summary>How long a check waits for something the app does on its own.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly FakeKeyboardHook hook;
    private readonly FakeChangeSource changes = new();
    private readonly SourceLocator sourceLocator = new();
    private readonly HttpClient http = new();
    private readonly PickerPresenter picker;
    private readonly CopyNotifier notifier;
    private readonly ClipboardMonitor monitor;
    private SettingsWindow? settingsWindow;

    private AppHarness(AppOptions options)
    {
        Options = options;
        Root = Path.Combine(Path.GetTempPath(), "shapeshifter-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = AppPaths.ForRoot(Root);
        Settings = new SettingsStore(Paths.SettingsFile, Logger.None);
        History = new ClipboardHistory(Settings.Current.MaxHistoryItems);
        Clipboard = new FakeClipboard();
        hook = new FakeKeyboardHook(options.Modifier);
        Paste = new FakePaste(Clipboard);
        monitor = new ClipboardMonitor(Clipboard, changes, History, Logger.None, sourceLocator) { Debounce = TimeSpan.FromMilliseconds(5) };
        Pins = new PinnedItemStore(Paths.PinnedDirectory, Logger.None);
        var context = new ActionContext
        {
            Clipboard = monitor,
            Paste = Paste,
            History = History,
            PinnedItems = Pins,
            Launcher = Launcher,
            Paths = Paths,
            Http = http,
            Logger = Logger.None,
        };
        picker = new PickerPresenter(new NoCursor(), new PickerVisuals(new DrawnFileIcons(), Logger.None), options.Modifier, Logger.None);
        notifier = new CopyNotifier(Settings);
        var coach = new CopyCoach(sourceLocator, () => options.Modifier, () => Settings.Current.CoachingTips, Time);
        Engine = new ShapeshifterEngine(History, monitor, hook, ActionCatalog.CreateDefault(), context, picker, notifier, Logger.None, Time, coach)
        {
            PasteDelay = TimeSpan.FromMilliseconds(Settings.Current.PasteDelayMilliseconds),
        };
    }

    public AppOptions Options { get; }

    /// <summary>A folder of its own, removed afterwards: the app's settings, pinned items and saved files live in it.</summary>
    public string Root { get; }

    public AppPaths Paths { get; }

    public SettingsStore Settings { get; }

    public ClipboardHistory History { get; }

    public PinnedItemStore Pins { get; }

    public ShapeshifterEngine Engine { get; }

    /// <summary>The engine's clock: the picker's show delay only elapses when a check advances it.</summary>
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    /// <summary>The system clipboard: what is on it now, and everything Shapeshifter wrote to it.</summary>
    public FakeClipboard Clipboard { get; }

    /// <summary>The paste keystrokes Shapeshifter sent, each with what was on the clipboard at that moment.</summary>
    public FakePaste Paste { get; }

    /// <summary>The URLs and folders Shapeshifter opened.</summary>
    public FakeLauncher Launcher { get; } = new();

    /// <summary>The picker's window, once it was first shown; <see cref="PickerVisible"/> says whether it is up.</summary>
    public PickerWindow? Picker => picker.Window;

    public bool PickerVisible => picker.IsVisible;

    /// <summary>The copy notification's window, once one was first shown.</summary>
    public ToastWindow? Notification => notifier.Window;

    /// <summary>The settings window, once <see cref="OpenSettings"/> opened it.</summary>
    public SettingsWindow? SettingsWindow => settingsWindow;

    /// <summary>Starts the app as <paramref name="options"/> say and runs <paramref name="test"/> against it, on the UI thread.</summary>
    public static Task RunAsync(Func<AppHarness, Task> test, AppOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(test);
        return TestApp.RunAsync(async () =>
        {
            options ??= new AppOptions();
            var culture = options.Language is null ? CultureInfo.GetCultureInfo("en") : CultureInfo.GetCultureInfo(options.Language);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            var application = Application.Current!;
            PlatformLook.Apply(application, options.Look, options.Dark ? ThemeVariant.Dark : ThemeVariant.Light);
            AccentPalette.Apply(application, Color.Parse(PlatformLook.Resource(application, "LookDefaultAccent", "#3584E4")));
            await using var app = new AppHarness(options);
            await app.Engine.StartAsync().ConfigureAwait(true);
            await test(app).ConfigureAwait(true);
        });
    }

    /// <summary>Copies <paramref name="text"/>, in the window <paramref name="window"/> (with an icon) or where none can be named.</summary>
    public Task CopyAsync(string text, string? window = null) => CopyAsync(new ClipboardSnapshot { Text = text }, window);

    /// <summary>Copies an image of <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
    public Task CopyImageAsync(int width, int height, string? window = null) =>
        CopyAsync(new ClipboardSnapshot { Image = Art.Picture(width, height) }, window);

    /// <summary>Copies files (or folders) together, by their full paths, as a file manager does.</summary>
    public Task CopyFilesAsync(params string[] paths) => CopyAsync(new ClipboardSnapshot { Files = paths }, null);

    /// <summary>Copies text with formatting (HTML, as a browser puts it on the clipboard beside the plain text).</summary>
    public Task CopyFormattedAsync(string text, string html, string? window = null) => CopyAsync(
        new ClipboardSnapshot { Text = text, RawFormats = [new RawClipboardFormat("text/html", System.Text.Encoding.UTF8.GetBytes(html))] }, window);

    /// <summary>Puts <paramref name="snapshot"/> on the clipboard as another application would, and waits until Shapeshifter has it.</summary>
    public async Task CopyAsync(ClipboardSnapshot snapshot, string? window)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        sourceLocator.Current = window is null ? null : new ClipboardSource(window, window, Art.WindowIcon());
        var reads = Clipboard.ReadCount;
        Clipboard.Current = snapshot;
        changes.Raise();
        await WaitUntilAsync(() => Clipboard.ReadCount > reads, "the clipboard was read").ConfigureAwait(true);
        await Task.Delay(50).ConfigureAwait(true);
        await Settle().ConfigureAwait(true);
    }

    /// <summary>Adds <paramref name="text"/> to the history as a pinned item, copied <paramref name="ago"/> ago.</summary>
    public async Task<ClipboardItem> PinAsync(string text, TimeSpan ago = default)
    {
        var item = new ClipboardItem(new ClipboardSnapshot { Text = text }, capturedAt: DateTimeOffset.UtcNow - ago);
        await Pins.SaveAsync(item).ConfigureAwait(true);
        History.AddPinned(item);
        return item;
    }

    /// <summary>Presses (and keeps down) <paramref name="key"/>: <see cref="GestureKey.Modifier"/> is Ctrl (or Cmd).</summary>
    public Task DownAsync(GestureKey key) => SendAsync(key, isDown: true);

    /// <summary>Lets go of <paramref name="key"/>.</summary>
    public Task UpAsync(GestureKey key) => SendAsync(key, isDown: false);

    /// <summary>Taps <paramref name="key"/>: down, then up.</summary>
    public async Task TapAsync(GestureKey key)
    {
        await DownAsync(key).ConfigureAwait(true);
        await UpAsync(key).ConfigureAwait(true);
    }

    /// <summary>Holds Ctrl (or Cmd) and taps V, keeping Ctrl down, until the picker appears.</summary>
    public async Task HoldCtrlVAsync()
    {
        await DownAsync(GestureKey.Modifier).ConfigureAwait(true);
        await DownAsync(GestureKey.Trigger).ConfigureAwait(true);
        Advance(Engine.PasteDelay + TimeSpan.FromMilliseconds(50));
        await UpAsync(GestureKey.Trigger).ConfigureAwait(true);
        await WaitUntilAsync(() => picker.IsVisible, "the picker appeared").ConfigureAwait(true);
        await Settle().ConfigureAwait(true);
    }

    /// <summary>Lets go of Ctrl (or Cmd), which pastes the choice, and waits until the picker is gone.</summary>
    public async Task ReleaseCtrlAsync()
    {
        await UpAsync(GestureKey.Modifier).ConfigureAwait(true);
        await WaitUntilAsync(() => !picker.IsVisible, "the picker closed").ConfigureAwait(true);
        await Task.Delay(100).ConfigureAwait(true);
        await Settle().ConfigureAwait(true);
    }

    /// <summary>Types <paramref name="query"/> into the picker's search box (start searching with <see cref="GestureKey.Search"/>).</summary>
    public async Task TypeSearchAsync(string query)
    {
        var box = Find<TextBox>(Picker ?? throw new InvalidOperationException("The picker was never shown."))
            .FirstOrDefault(textBox => textBox.IsEffectivelyVisible) ?? throw new InvalidOperationException("The picker shows no search box.");
        box.Text = query;
        await Task.Delay(50).ConfigureAwait(true);
        await Settle().ConfigureAwait(true);
    }

    /// <summary>Moves the engine's clock on, as when the user keeps keys held for that long.</summary>
    public void Advance(TimeSpan by) => Time.Advance(by);

    /// <summary>Opens the settings window as the tray icon's Settings… does (the same window again when it is open).</summary>
    public async Task<SettingsWindow> OpenSettings()
    {
        if (settingsWindow is null)
        {
            var modifiers = Options.Look switch
            {
                Look.Windows => new[] { "Ctrl", "Win" },
                Look.MacOS => ["Cmd", "Ctrl"],
                _ => ["Ctrl"],
            };
            settingsWindow = new SettingsWindow
            {
                DataContext = new SettingsViewModel(Settings, new FakeAutostart(), History, Launcher, Paths, () => Options.Modifier, [], shortcutModifierNames: modifiers)
                {
                    ShowsUpdates = !Options.StoreCopy,
                },
            };
            settingsWindow.Closed += (_, _) => settingsWindow = null;
        }

        settingsWindow.Show();
        settingsWindow.Activate();
        await Settle().ConfigureAwait(true);
        return settingsWindow;
    }

    /// <summary>Every piece of text <paramref name="root"/> shows to the user, in reading order, as rendered.</summary>
    public static IReadOnlyList<string> Texts(Visual root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root.GetVisualDescendants()
            .Where(visual => visual.IsEffectivelyVisible)
            .Select(visual => visual switch
            {
                TextBlock block => block.Text,
                Avalonia.Controls.Presenters.TextPresenter presenter => presenter.Text,
                _ => null,
            })
            .Where(text => !string.IsNullOrEmpty(text))
            .Select(text => text!)
            .ToArray();
    }

    /// <summary>The visible controls of type <typeparamref name="T"/> inside <paramref name="root"/>.</summary>
    public static IReadOnlyList<T> Find<T>(Visual root)
        where T : Visual
    {
        ArgumentNullException.ThrowIfNull(root);
        return root.GetVisualDescendants().OfType<T>().Where(visual => visual.IsEffectivelyVisible).ToArray();
    }

    /// <summary>
    /// Saves what <paramref name="window"/> shows as a PNG, when the environment variable AIL_SHOTS_DIR is set (ail sets it
    /// to collect each scenario's final screen): into that folder, as <paramref name="name"/> (the file name ail gives,
    /// such as "a-copy-on-linux.png"; a scenario's title is turned into it). Does nothing when AIL_SHOTS_DIR is not set.
    /// </summary>
    public static void SaveScreen(Window window, string name)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(name);
        var folder = Environment.GetEnvironmentVariable("AIL_SHOTS_DIR");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        var file = name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? name : ShotName(name);
        // Greyscale antialiasing, as the generator renders: subpixel antialiasing depends on the screen.
        TextOptions.SetTextRenderingMode(window, TextRenderingMode.Antialias);
        RenderOptions.SetBitmapInterpolationMode(window, BitmapInterpolationMode.HighQuality);
        for (var i = 0; i < 10; i++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window rendered nothing.");
        Directory.CreateDirectory(folder);
        frame.Save(Path.Combine(folder, file), new PngBitmapEncoderOptions());
    }

    /// <summary>The file name ail gives a scenario's screen: its title in lower case, each run of other characters one "-".</summary>
    public static string ShotName(string scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        var slug = Regex.Replace(scenario.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return (slug.Length > 60 ? slug[..60] : slug is "" ? "test" : slug) + ".png";
    }

    /// <summary>Waits until <paramref name="condition"/> holds, letting the UI thread work meanwhile.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string what)
    {
        ArgumentNullException.ThrowIfNull(condition);
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Timed out waiting until " + what);
            }

            await Task.Delay(10).ConfigureAwait(true);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Engine.DisposeAsync().ConfigureAwait(true);
        await picker.HideAsync().ConfigureAwait(true);
        notifier.Window?.Hide();
        settingsWindow?.Close();
        monitor.Dispose();
        http.Dispose();
        await Settle().ConfigureAwait(true);
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task SendAsync(GestureKey key, bool isDown)
    {
        hook.Send(key, isDown);
        await Task.Delay(30).ConfigureAwait(true);
        await Settle().ConfigureAwait(true);
    }

    /// <summary>Lets the engine and the UI thread finish what they started: layout, rendering, posted work.</summary>
    private static async Task Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            await Task.Delay(10).ConfigureAwait(true);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class SourceLocator : IClipboardSourceLocator
    {
        public ClipboardSource? Current { get; set; }

        public ClipboardSource? GetCurrentSource() => Current;
    }

    /// <summary>No pointer: the picker goes on the primary screen.</summary>
    private sealed class NoCursor : ICursorLocator
    {
        public (int X, int Y)? GetCursorPosition() => null;
    }

    private sealed class FakeAutostart : IAutostartService
    {
        public bool IsSupported => true;

        public bool IsEnabled { get; private set; }

        public void SetEnabled(bool enabled) => IsEnabled = enabled;
    }
}
