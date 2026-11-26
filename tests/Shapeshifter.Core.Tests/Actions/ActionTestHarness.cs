using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Persistence;
using Shapeshifter.Core.Platform;
using Shapeshifter.Core.Settings;

namespace Shapeshifter.Core.Tests.Actions;

/// <summary>Records every clipboard write; reading returns the last written snapshot.</summary>
internal sealed class RecordingClipboardBackend(List<string> eventLog) : IClipboardBackend
{
    public List<ClipboardSnapshot> Writes { get; } = [];

    public ClipboardSnapshot Current { get; set; } = ClipboardSnapshot.Empty;

    public Task<ClipboardSnapshot> ReadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

    public Task WriteAsync(ClipboardSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        lock (eventLog)
        {
            eventLog.Add("write");
        }

        Writes.Add(snapshot);
        Current = snapshot;
        return Task.CompletedTask;
    }

    public Task<string> GetChangeTokenAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Current.Fingerprint);
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

internal sealed class CountingPasteSimulator(List<string> eventLog) : IPasteSimulator
{
    public int Calls { get; private set; }

    public Task SimulatePasteAsync(CancellationToken cancellationToken = default)
    {
        lock (eventLog)
        {
            eventLog.Add("paste");
        }

        Calls++;
        return Task.CompletedTask;
    }
}

internal sealed class RecordingLauncher : ILauncher
{
    public List<Uri> OpenedUrls { get; } = [];

    public List<string> OpenedFolders { get; } = [];

    public void OpenUrl(Uri uri) => OpenedUrls.Add(uri);

    public void OpenFolder(string path) => OpenedFolders.Add(path);
}

internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
        _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public List<Uri?> RequestedUris { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestedUris.Add(request.RequestUri);
        return Task.FromResult(Respond(request));
    }
}

/// <summary>Content whose length is not known up front (like a chunked response).</summary>
internal sealed class UnknownLengthContent : HttpContent
{
    private readonly byte[] data;

    public UnknownLengthContent(byte[] data, string? mediaType)
    {
        this.data = data;
        if (mediaType is not null)
        {
            Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        }
    }

    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
        stream.WriteAsync(data, 0, data.Length);

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}

/// <summary>A real <see cref="ActionContext"/> wired to fakes and a private temporary folder.</summary>
internal sealed class ActionTestHarness : IDisposable
{
    public ActionTestHarness()
    {
        Root = Directory.CreateTempSubdirectory("shapeshifter-actions-").FullName;
        Paths = AppPaths.ForRoot(Root);
        Backend = new RecordingClipboardBackend(EventLog);
        ChangeSource = new FakeChangeSource();
        History = new ClipboardHistory();
        Monitor = new ClipboardMonitor(Backend, ChangeSource, History);
        Paste = new CountingPasteSimulator(EventLog);
        PinnedItems = new PinnedItemStore(Paths.PinnedDirectory);
        Launcher = new RecordingLauncher();
        Handler = new FakeHttpMessageHandler();
        Http = new HttpClient(Handler);
        Context = new ActionContext
        {
            Clipboard = Monitor,
            Paste = Paste,
            History = History,
            PinnedItems = PinnedItems,
            Launcher = Launcher,
            Paths = Paths,
            Http = Http,
        };
    }

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public List<string> EventLog { get; } = [];

    public string Root { get; }

    public AppPaths Paths { get; }

    public RecordingClipboardBackend Backend { get; }

    public FakeChangeSource ChangeSource { get; }

    public ClipboardHistory History { get; }

    public ClipboardMonitor Monitor { get; }

    public CountingPasteSimulator Paste { get; }

    public PinnedItemStore PinnedItems { get; }

    public RecordingLauncher Launcher { get; }

    public FakeHttpMessageHandler Handler { get; }

    public HttpClient Http { get; }

    public ActionContext Context { get; }

    /// <summary>Adds <paramref name="snapshot"/> to the history as a captured item and returns it.</summary>
    public ClipboardItem AddToHistory(ClipboardSnapshot snapshot) => History.Add(snapshot);

    public void Dispose()
    {
        Monitor.Dispose();
        Http.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Representative items of every kind.</summary>
internal static class Items
{
    public static readonly TheoryData<string> AllKinds =
    [
        "plain", "whitespace", "padded", "text+html", "html-only", "empty-text+html", "rtf-only", "image", "file", "files", "other",
        "base64", "url-encoded", "json", "color-hex", "unix-time", "iso-date", "sum", "email", "empty", "link", "image-link", "text+image", "file+html",
    ];

    public static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static ClipboardSnapshot Snapshot(string name) => name switch
    {
        "plain" => new() { Text = "hello world" },
        "whitespace" => new() { Text = "   " },
        "padded" => new() { Text = "  hello world\n" },
        "text+html" => new()
        {
            Text = "Hello World",
            RawFormats = [new RawClipboardFormat("text/html", Utf8("<b>Hello</b> World"))],
        },
        "html-only" => new() { RawFormats = [new RawClipboardFormat("text/html", Utf8("<p>Hello</p><p>World</p>"))] },
        "empty-text+html" => new()
        {
            Text = string.Empty,
            RawFormats = [new RawClipboardFormat("HTML Format", Utf8("<p>Hello</p><p>World</p>"))],
        },
        "rtf-only" => new() { RawFormats = [new RawClipboardFormat("text/rtf", Utf8(@"{\rtf1\ansi Hello World}"))] },
        "image" => new() { Image = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3] },
        "file" => new() { Files = ["/tmp/does-not-matter/a.txt"] },
        "files" => new() { Files = ["/tmp/x/a.txt", "/tmp/x/b.txt"] },
        "other" => new() { RawFormats = [new RawClipboardFormat("application/x-custom", [1, 2, 3])] },
        "base64" => new() { Text = "aGVsbG8gd29ybGQ=" },
        "url-encoded" => new() { Text = "a%20b" },
        "json" => new() { Text = "{\"a\":1}" },
        "color-hex" => new() { Text = "#3584e4" },
        "unix-time" => new() { Text = "1795703400" },
        "iso-date" => new() { Text = "2026-11-26T14:30:00Z" },
        "sum" => new() { Text = "12*7+3" },
        "email" => new() { Text = "anna@example.com" },
        "empty" => ClipboardSnapshot.Empty,
        "link" => new() { Text = "see https://example.com/page" },
        "image-link" => new() { Text = "see https://example.com/pic.png" },
        "text+image" => new() { Text = "caption https://example.com/pic.png", Image = [1, 2] },
        "file+html" => new()
        {
            Files = ["/tmp/x/a.txt"],
            RawFormats = [new RawClipboardFormat("text/html", Utf8("<b>x</b>"))],
        },
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, null),
    };

    public static ClipboardItem ByName(string name) => new(Snapshot(name));

    public static ClipboardItem LinkText(string text) => new(new ClipboardSnapshot { Text = text });
}
