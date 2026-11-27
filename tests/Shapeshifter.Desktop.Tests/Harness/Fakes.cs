using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Platform;
using Shapeshifter.Core.Settings;
using Shapeshifter.ScreenshotGenerator;

namespace Shapeshifter.Desktop.Tests;

/// <summary>The system clipboard: what another application put on it, and what Shapeshifter wrote to it.</summary>
public sealed class FakeClipboard : IClipboardBackend
{
    private readonly Lock gate = new();
    private readonly List<ClipboardSnapshot> written = [];
    private ClipboardSnapshot current = ClipboardSnapshot.Empty;
    private int readCount;

    /// <summary>What is on the clipboard now.</summary>
    public ClipboardSnapshot Current
    {
        get { lock (gate) { return current; } }
        set { lock (gate) { current = value; } }
    }

    /// <summary>Everything Shapeshifter wrote to the clipboard, in order.</summary>
    public IReadOnlyList<ClipboardSnapshot> Written
    {
        get { lock (gate) { return written.ToArray(); } }
    }

    public int ReadCount => Volatile.Read(ref readCount);

    public Task<ClipboardSnapshot> ReadAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref readCount);
        return Task.FromResult(Current);
    }

    public Task WriteAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            current = snapshot;
            written.Add(snapshot);
        }

        return Task.CompletedTask;
    }

    public Task<string> GetChangeTokenAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current.Fingerprint);
}

/// <summary>The paste keystrokes Shapeshifter sent to the application in front, each with what the clipboard held then.</summary>
public sealed class FakePaste(FakeClipboard clipboard) : IPasteSimulator
{
    private readonly List<ClipboardSnapshot> pasted = [];

    /// <summary>What each paste put into the application in front, in order: empty when nothing was pasted.</summary>
    public IReadOnlyList<ClipboardSnapshot> Pasted
    {
        get { lock (pasted) { return pasted.ToArray(); } }
    }

    public Task SimulatePasteAsync(CancellationToken cancellationToken = default)
    {
        lock (pasted)
        {
            pasted.Add(clipboard.Current);
        }

        return Task.CompletedTask;
    }
}

/// <summary>The URLs, folders and files Shapeshifter asked the system to open.</summary>
public sealed class FakeLauncher : ILauncher
{
    public List<Uri> OpenedUrls { get; } = [];

    public List<string> OpenedFolders { get; } = [];

    public List<string> RevealedPaths { get; } = [];

    public void OpenUrl(Uri uri) => OpenedUrls.Add(uri);

    public void OpenFolder(string path) => OpenedFolders.Add(path);

    public void RevealInFileManager(string path) => RevealedPaths.Add(path);
}

internal sealed class FakeChangeSource : IClipboardChangeSource
{
    public event EventHandler? Changed;

    public void Start()
    {
    }

    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
    }
}

/// <summary>The global keyboard hook, fed by the check instead of the keyboard.</summary>
internal sealed class FakeKeyboardHook(string modifierName) : IKeyboardHook
{
    public Func<KeyInput, bool>? KeyHandler { get; set; }

    public string ModifierName => modifierName;

    public PasteShortcut PasteShortcut { get; set; }

    public IReadOnlyList<string> ShortcutModifierNames => [modifierName];

    public bool GestureModifierPastes => true;

    public void SetNavigationCaptureEnabled(bool enabled)
    {
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public bool Send(GestureKey key, bool isDown) =>
        (KeyHandler ?? throw new InvalidOperationException("The engine has not started."))(new KeyInput(key, isDown));

    public void Dispose()
    {
    }
}

/// <summary>File and folder icons, drawn here (as in the screenshots) rather than taken from a system theme.</summary>
internal sealed class DrawnFileIcons : IFileIconProvider
{
    public byte[]? GetIconPng(string path, int size)
    {
        if (path.EndsWith('/') || string.IsNullOrEmpty(Path.GetExtension(path)))
        {
            return SampleArt.Folder(size * 2);
        }

        return Path.GetExtension(path).ToUpperInvariant() switch
        {
            ".MP4" => SampleArt.Document(size * 2, SampleArt.DocumentBadge.Video),
            ".PNG" or ".JPG" => SampleArt.Document(size * 2, SampleArt.DocumentBadge.Picture),
            _ => SampleArt.Document(size * 2, SampleArt.DocumentBadge.Lines),
        };
    }
}

/// <summary>Pictures for checks: copied images and the icons of the windows things are copied in.</summary>
internal static class Art
{
    /// <summary>A colourful picture of <paramref name="width"/> × <paramref name="height"/> pixels, as a PNG.</summary>
    public static byte[] Picture(int width, int height) => SampleArt.Banner(width, height);

    /// <summary>The icon of a window things are copied in.</summary>
    public static byte[] WindowIcon() => SampleArt.EditorIcon();
}
