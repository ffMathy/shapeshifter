// ail-derived-from: KeepFormats@9a2b7088dbf996f7
// End-to-end check for KeepFormats: an item picked from the history puts back on the clipboard what was copied,
// format by format. Another app's view of the clipboard is read with xclip after the paste.
using System.Text;
using static Shapeshifter.Checks.CaptureDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class KeepFormatsChecks(CaptureAppFixture target) : Scenario, IClassFixture<CaptureAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); await WaitForNoNotification(); }

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    [Fact(DisplayName = "rich text keeps its HTML", Timeout = 30_000)]
    public async Task RichTextKeepsItsHtml()
    {
        var id = Unique();
        var html = $"<p>Rich <b>bold {id}</b> and <i>italic</i></p>";
        Assert.Equal("OK", await App.Send($"copy-html {B64($"Rich bold {id} and italic")} {B64(html)}"));
        await Sleep(800);
        await App.Copy($"plain after rich {id}");
        Assert.Equal($"Rich bold {id} and italic", await PasteFromHistoryShowingIt(App, "v"));
        Assert.Contains("text/html", ClipboardTypes());
        Assert.Equal(html, Encoding.UTF8.GetString(ClipboardBytes("text/html")));
    }

    [Fact(DisplayName = "copied files are kept as files", Timeout = 30_000)]
    public async Task CopiedFilesAreKeptAsFiles()
    {
        var dir = Path.Combine(Scratch, $"files-{Unique()}");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "report.txt");
        await File.WriteAllTextAsync(file, "a file to copy", TestContext.Current.CancellationToken);
        await XCopy($"file://{file}\n", "text/uri-list");
        await App.Copy($"text after file {Unique()}");
        await PasteFromHistoryShowingIt(App, "v");
        Assert.Contains("text/uri-list", ClipboardTypes());
        Assert.Equal($"file://{file}", Encoding.UTF8.GetString(ClipboardBytes("text/uri-list")).Trim());
    }

    [Fact(DisplayName = "another app's own format is kept byte for byte", Timeout = 30_000)]
    public async Task OwnFormatIsKeptByteForByte()
    {
        byte[] bytes = [0, 1, 2, 250, 255, 13, 10, .. Encoding.UTF8.GetBytes(Unique())];
        await XCopy(bytes, "application/x-ail-capture-check");
        await App.Copy($"text after custom {Unique()}");
        await PasteFromHistoryShowingIt(App, "v");
        Assert.Contains("application/x-ail-capture-check", ClipboardTypes());
        Assert.Equal(bytes, ClipboardBytes("application/x-ail-capture-check"));
    }

    [Fact(DisplayName = "an image is kept, and other apps can read it back", Timeout = 40_000)]
    public async Task AnImageIsKept()
    {
        var png = Path.Combine(Scratch, $"image-{Unique()}.png");
        Desktop.Run("convert", ["-size", "4x3", "xc:red", "-fill", "blue", "-draw", "point 1,1", "-fill", "lime", "-draw", "point 2,2", png]);
        await XCopy(await File.ReadAllBytesAsync(png, TestContext.Current.CancellationToken), "image/png");
        await App.Copy($"text after image {Unique()}");
        await PasteFromHistoryShowingIt(App, "v");
        Assert.Contains("image/png", ClipboardTypes());
        // Read twice, as an app asking for the image again would: same pixels both times.
        foreach (var n in new[] { 1, 2 })
        {
            var back = Path.Combine(Scratch, $"back-{n}-{Unique()}.png");
            await File.WriteAllBytesAsync(back, ClipboardBytes("image/png"), TestContext.Current.CancellationToken);
            Assert.True(SamePixels(png, back), $"read back the {n}. time, the image's pixels differ from what was copied");
        }
        // Shapeshifter is still there: the next copy is in the history.
        var id = Unique();
        await App.Copy($"still running {id}");
        Assert.Equal($"still running {id}", await PasteFromHistory(App));
        Assert.NotEqual($"still running {id}", await PasteFromHistory(App, "v"));
    }

    [Fact(DisplayName = "files copied in a Remote Desktop session are kept as files", Timeout = 40_000)]
    public async Task RemoteDesktopFilesAreKeptAsFiles()
    {
        // Remote Desktop offers files copied in a session as virtual files (FileGroupDescriptorW and one FileContents
        // stream per file), a Windows clipboard format; the target app can only serve them on Windows.
        Assert.SkipUnless(OperatingSystem.IsWindows(), "virtual files, as Remote Desktop copies files, are a Windows clipboard format");
        var id = Unique();
        var name = $"report-{id}.txt";
        var contents = $"remote {id}";
        Assert.Equal("OK", await App.Send($"copy-virtual-file {B64(name)} {B64(contents)}"));
        await Sleep(800);
        // Copying something else takes the virtual files off the clipboard, so their streams can no longer be read.
        await App.Copy($"text after remote file {id}");
        await PasteFromHistoryShowingIt(App, "v");
        var answer = (await App.Send("read-files")).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("FILES", answer[0]);
        var files = answer.Skip(1).Chunk(2).Select(f => (Name: Decode(f[0]), Contents: Decode(f[1]))).ToArray();
        Assert.Equal([(name, contents)], files);
    }

    static string Decode(string b64) => Encoding.UTF8.GetString(Convert.FromBase64String(b64));
}
