// ail-derived-from: PasteActions@dd5da39c2cfb3f8d
// End-to-end check for PasteActions: Paste, Paste as plain text and Paste as file, seen through what arrives in
// another app, the clipboard (xclip) and the files the user is handed.
using System.Text;
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class PasteActionsChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    async Task CopyHtml(string text, string html)
    {
        Assert.Equal("OK", await App.Send($"copy-html {B64(text)} {B64(html)}"));
        await Sleep(1200);
    }

    [Fact(DisplayName = "Paste puts back every format", Timeout = 30_000)]
    public async Task PastePutsBackEveryFormat()
    {
        var id = Unique();
        await CopyHtml($"rich {id}", $"<b>rich</b> {id}");
        await App.Copy($"plain {id}");
        await App.Prepare();
        await OpenPicker(1);
        await ChooseAction(0);
        Assert.Equal($"rich {id}", await App.WaitForText($"rich {id}"));
        Assert.Contains("text/html", ClipboardFormats());
        Assert.Contains($"<b>rich</b> {id}", ClipboardAs("text/html"));
    }

    [Fact(DisplayName = "Paste as plain text leaves the formatting out", Timeout = 30_000)]
    public async Task PasteAsPlainText()
    {
        var id = Unique();
        await CopyHtml($"styled {id}", $"<i>styled</i> {id}");
        await App.Prepare();
        await OpenPicker();
        await ChooseAction(2);
        Assert.Equal($"styled {id}", await App.WaitForText($"styled {id}"));
        Assert.DoesNotContain("text/html", ClipboardFormats());
        Assert.Equal($"styled {id}", ClipboardAs("UTF8_STRING"));
    }

    [Fact(DisplayName = "Paste as file turns text into a .txt file", Timeout = 30_000)]
    public async Task PasteTextAsFile()
    {
        var id = Unique();
        await App.Copy($"notes {id}\nsecond line");
        await App.Prepare();
        await OpenPicker();
        await ChooseAction(1);
        Assert.Contains("text/uri-list", ClipboardFormats());
        var files = ClipboardFiles();
        Assert.Single(files);
        Assert.Matches(@"/Text \d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d\.txt$", files[0]);
        Assert.Equal($"notes {id}\nsecond line", await File.ReadAllTextAsync(files[0], TestContext.Current.CancellationToken));
        Assert.Equal("", await App.Text());
    }

    [Fact(DisplayName = "Paste as file turns an image into an image file", Timeout = 30_000)]
    public async Task PasteImageAsFile()
    {
        var width = 41 + Random.Shared.Next(50);
        await CopyAs("image/png", MakePng(width, 17, "blue"));
        await App.Prepare();
        await OpenPicker();
        // An image also offers "Upload and paste link", which would send it to a public host: never land on it.
        // Find "Paste as file" on screen first, and cancel unless it is exactly where it should be.
        await Tap("Right");
        await Sleep(300);
        var titles = ActionTitlesOnScreen();
        if (Array.IndexOf(titles, "Paste as file") != 1 || Array.IndexOf(titles, "Upload and paste link") == 1)
        {
            await Tap("Escape");
            await KeyUp("ctrl");
            Assert.Fail($"unexpected actions for an image: {string.Join(", ", titles)}");
        }
        await Tap("Down");
        await Sleep(200);
        await KeyUp("ctrl");
        await Sleep(1200);
        var files = ClipboardFiles();
        Assert.Single(files);
        Assert.Matches(@"/Image \d{4}-\d\d-\d\d \d\d\.\d\d\.\d\d\.png$", files[0]);
        Assert.Equal($"{width} 17", Run("identify", ["-format", "%w %h", files[0]]));
    }
}
