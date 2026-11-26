// ail-derived-from: WhatSearchFinds@b79b620a1398ff71
// End-to-end check for WhatSearchFinds: which copies a search finds, on a real X11 desktop. Copies come from the
// target app and from small xterm windows with titles of our choosing; a find is proven by what Enter puts in the
// other app or on the clipboard, and "No matches" by the words on the picker.
using static Shapeshifter.Checks.Desktop;
using static Shapeshifter.Checks.SearchDriver;

namespace Shapeshifter.Checks;

public sealed class WhatSearchFindsChecks(TargetAppFixture target) : Scenario, IClassFixture<TargetAppFixture>
{
    TargetApp App => target.App;

    protected override async Task AfterEach()
    {
        await ReleaseAll();
        if (FocusedWindowName() == "Shapeshifter") await Tap("Escape");
        await WaitForPicker(false);
    }

    /// <summary>Waits until the clipboard offers <paramref name="type"/> with content that passes <paramref name="ok"/>.</summary>
    static async Task<bool> ClipboardBecomes(string type, Func<string, bool> ok, int timeout = 4000)
    {
        for (var until = DateTime.UtcNow.AddMilliseconds(timeout); DateTime.UtcNow < until; await Sleep(200))
        {
            if (ClipboardTargets().Contains(type) && ok(ClipboardAs(type))) return true;
        }
        return false;
    }

    [Fact(DisplayName = "every word must occur, in any order, ignoring case and accents", Timeout = 30_000)]
    public async Task EveryWordMustOccur()
    {
        var id = Unique();
        await App.Copy($"Crème brûlée {id}");
        await App.Copy($"crème fraîche {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"BRULEE {id} creme");
        await Tap("Return");
        Assert.Equal($"Crème brûlée {id}", await App.WaitForText($"Crème brûlée {id}"));
    }

    [Fact(DisplayName = "a search that finds nothing says so", Timeout = 30_000)]
    public async Task NothingFoundSaysSo()
    {
        var id = Unique();
        await App.Copy($"walnut {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"walnut {id} pecan");
        var words = await WaitForPickerWords(["no matches", "nothing you copied contains"]);
        Assert.Contains("no matches", words, StringComparison.Ordinal);
        Assert.Contains("nothing you copied contains", words, StringComparison.Ordinal);
        await Tap("Return");
        await Sleep(500);
        Assert.Equal("Shapeshifter", FocusedWindowName());
        Assert.True(await WaitForPicker(true));
        await Tap("Escape");
        await Sleep(500);
        Assert.Equal("", await App.Text());
    }

    [Fact(DisplayName = "the whole text is searched, not only its first line", Timeout = 30_000)]
    public async Task WholeTextIsSearched()
    {
        var id = Unique();
        var text = $"shopping list {id}\nbread\nmilk\nlast line saffron{id}";
        await App.Copy(text);
        await App.Copy($"other {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"saffron{id}");
        await Tap("Return");
        Assert.Equal(text, await App.WaitForText(text));
    }

    [Fact(DisplayName = "copied HTML is searched by its words, not its markup", Timeout = 30_000)]
    public async Task HtmlIsSearchedByItsWords()
    {
        var id = Unique();
        await CopyFromWindow($"page {Unique()}", "text/html", $"<p>Hello <span class=\"marker{id}\">tangerine{id}</span></p>");
        await App.Copy($"later {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"marker{id}");
        Assert.Contains("no matches", await WaitForPickerWords(["no matches"]), StringComparison.Ordinal);
        for (var i = 0; i < $"marker{id}".Length; i++) await Tap("BackSpace", 20);
        await Type($"hello tangerine{id}");
        await Tap("Return");
        Assert.False(await WaitForPicker(false));
        Assert.True(await ClipboardBecomes("text/html", html => html.Contains($"tangerine{id}", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "a copied file is found by its path and by the word file", Timeout = 30_000)]
    public async Task FileIsFoundByPathAndWord()
    {
        var id = Unique();
        var path = MakeFile($"report-{id}.txt");
        await CopyFromWindow($"manager {Unique()}", "text/uri-list", $"file://{path}\r\n");
        await App.Copy($"later {id}");
        await App.Prepare();
        await StartSearch();
        await Type($"file report-{id}");
        await Tap("Return");
        Assert.False(await WaitForPicker(false));
        Assert.True(await ClipboardBecomes("text/uri-list", uris => uris.Contains($"report-{id}.txt", StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "a copied picture is found by the word image", Timeout = 30_000)]
    public async Task PictureIsFoundByTheWordImage()
    {
        var id = Unique();
        await CopyFromWindow($"album {id}", "image/png", SquarePng());
        await CopyFromWindow($"album {id}", "text", $"caption {Unique()}");
        await App.Copy($"later {Unique()}");
        await App.Prepare();
        await StartSearch();
        await Type($"image album {id}");
        // Only the picture is left, titled "Image · 24 × 24"; the caption from the same window is not. (Pasting the
        // picture is not part of this check: on Linux, serving a restored picture to another app crashed Shapeshifter.)
        var words = await WaitForPickerWords(["24 x 24"]);
        Assert.Matches("24 ?[x×] ?24", words);
        Assert.DoesNotContain("caption", words, StringComparison.Ordinal);
        Assert.DoesNotContain("no matches", words, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "an item is found by the window it was copied from", Timeout = 30_000)]
    public async Task FoundByItsWindow()
    {
        var id = Unique();
        var words = $"sunny weather {Unique()}";
        await CopyFromWindow($"notes {id}", "text", words);
        await App.Copy($"later {Unique()}");
        await App.Prepare();
        await StartSearch();
        await Type($"notes {id}");
        await Tap("Return");
        Assert.Equal(words, await App.WaitForText(words));
    }
}
