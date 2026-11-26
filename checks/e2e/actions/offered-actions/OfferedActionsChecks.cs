// ail-derived-from: OfferedActions@e319a338d3dd92f6
// End-to-end check for OfferedActions: which actions each kind of item offers, in which order, as read from the
// screen (OCR of the picker's actions column). Content is put on the clipboard with xclip, as another app would.
using System.Text;
using static Shapeshifter.Checks.ActionsDriver;
using static Shapeshifter.Checks.Desktop;

namespace Shapeshifter.Checks;

public sealed class OfferedActionsChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach() { await ReleaseAll(); await WaitForPicker(false); }

    /// <summary>Opens the actions of the newest item, reads them, and cancels.</summary>
    static async Task<string[]> ActionsOfNewest()
    {
        await OpenPicker();
        await Tap("Right");
        await Sleep(300);
        var titles = ActionTitlesOnScreen();
        await Tap("Escape");
        await KeyUp("ctrl");
        return titles;
    }

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    static string Files(params string[] names)
    {
        var dir = Directory.CreateTempSubdirectory("ail-offered-").FullName;
        return string.Join("\n", names.Select(n => { var p = Path.Combine(dir, n); File.WriteAllText(p, n); return $"file://{p}"; })) + "\n";
    }

    [Fact(DisplayName = "plain text offers Paste, Paste as file, Upload and paste link, Search the web, and Pin", Timeout = 30_000)]
    public async Task PlainText()
    {
        await App.Copy($"plain {Unique()}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "formatted text also offers Paste as plain text and Paste as Markdown", Timeout = 30_000)]
    public async Task FormattedText()
    {
        var id = Unique();
        Assert.Equal("OK", await App.Send($"copy-html {B64($"bold {id}")} {B64($"<b>bold</b> {id}")}"));
        await Sleep(1200);
        Assert.Equal(["Paste", "Paste as file", "Paste as plain text", "Paste as Markdown", "Upload and paste link", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "text with spaces or blank lines around it also offers Paste trimmed", Timeout = 30_000)]
    public async Task PaddedText()
    {
        await CopyAs("UTF8_STRING", $"  padded {Unique()} \n");
        Assert.Equal(["Paste", "Paste as file", "Paste trimmed", "Upload and paste link", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "Base64 of a readable text also offers Paste decoded Base64", Timeout = 30_000)]
    public async Task Base64Text()
    {
        await CopyAs("UTF8_STRING", B64($"hello {Unique()}"));
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste decoded Base64", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "a text with %-codes also offers Paste URL-decoded", Timeout = 30_000)]
    public async Task UrlEncodedText()
    {
        await CopyAs("UTF8_STRING", $"a%20{Unique()}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste URL-decoded", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "JSON on one line also offers Paste formatted JSON, and JSON over several lines Paste minified JSON", Timeout = 40_000)]
    public async Task Json()
    {
        var id = Unique();
        await CopyAs("UTF8_STRING", $"{{\"{id}\":1}}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste formatted JSON", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
        await CopyAs("UTF8_STRING", $"{{\n  \"{id}\": 2\n}}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste minified JSON", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "a colour also offers it in the other notations", Timeout = 30_000)]
    public async Task Colour()
    {
        await CopyAs("UTF8_STRING", $"#{Random.Shared.Next(0x1000000):x6}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste as RGB colour", "Paste as HSL colour", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "a Unix time also offers Paste as date and time, and a date and time Paste as Unix time", Timeout = 40_000)]
    public async Task Timestamps()
    {
        var seconds = 1_700_000_000 + Random.Shared.Next(100_000_000);
        await CopyAs("UTF8_STRING", seconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste as date and time", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
        await CopyAs("UTF8_STRING", DateTimeOffset.FromUnixTimeSeconds(seconds).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste as Unix time", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "a calculation also offers Paste result", Timeout = 30_000)]
    public async Task Calculation()
    {
        await CopyAs("UTF8_STRING", $"{Random.Shared.Next(2, 99)}*{Random.Shared.Next(2, 99)}+{Random.Shared.Next(2, 99)}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Paste result", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "an e-mail address also offers Write email", Timeout = 30_000)]
    public async Task EmailAddress()
    {
        await CopyAs("UTF8_STRING", $"anna.{Unique()}@example.com");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Write email", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "the path of an existing file also offers Copy file at this path and Show in file manager", Timeout = 30_000)]
    public async Task FilePathText()
    {
        var path = Path.Combine(Directory.CreateTempSubdirectory("ail-offered-").FullName, $"report-{Unique()}.txt");
        File.WriteAllText(path, "report");
        await CopyAs("UTF8_STRING", path);
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Copy file at this path", "Search the web", "Show in file manager", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "an image offers Paste, Paste as file, Upload and paste link, and Pin", Timeout = 30_000)]
    public async Task Image()
    {
        await CopyAs("image/png", MakePng(31 + Random.Shared.Next(40), 23));
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "a file offers Paste, Paste path, Paste as compressed file and Pin", Timeout = 30_000)]
    public async Task OneFile()
    {
        await CopyAs("text/uri-list", Files($"one-{Unique()}.txt"));
        Assert.Equal(["Paste", "Paste path", "Paste as compressed file", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "several files offer the same as one file, with Paste paths", Timeout = 30_000)]
    public async Task SeveralFiles()
    {
        await CopyAs("text/uri-list", Files($"a-{Unique()}.txt", $"b-{Unique()}.txt"));
        Assert.Equal(["Paste", "Paste paths", "Paste as compressed file", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "other data offers only Paste and Pin", Timeout = 30_000)]
    public async Task OtherData()
    {
        await CopyAs($"application/x-ail-{Unique()}", $"data {Unique()}");
        Assert.Equal(["Paste", "Pin to clipboard"], await ActionsOfNewest());
    }

    [Fact(DisplayName = "text with links also offers the link actions", Timeout = 30_000)]
    public async Task TextWithLinks()
    {
        var id = Unique();
        await CopyAs("UTF8_STRING", $"see https://example.com/{id}.png and https://example.org/{id}");
        Assert.Equal(["Paste", "Paste as file", "Upload and paste link", "Copy image from link", "Open links", "Search the web", "Pin to clipboard"], await ActionsOfNewest());
    }
}
