// ail-derived-from: SearchWeb@ee6da03e2e137b0e
using System;
using System.Globalization;
using System.Threading.Tasks;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the SearchWeb node: the "Search the web" action.</summary>
public sealed class SearchWebTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    private static void AssertLink(string expected, Uri? actual)
    {
        Assert.NotNull(actual);
        Assert.True(actual.IsAbsoluteUri);
        Assert.Equal(expected, actual.AbsoluteUri);
    }

    [Fact(DisplayName = "example 1: SearchUrl(\"shapeshifter clipboard\") => https://duckduckgo.com/?q=shapeshifter%20clipboard")]
    public void Example1PlainWords()
    {
        AssertLink("https://duckduckgo.com/?q=shapeshifter%20clipboard", SearchWebAction.SearchUrl("shapeshifter clipboard"));
    }

    [Fact(DisplayName = "example 2: SearchUrl(\"  NullReferenceException  \") => https://duckduckgo.com/?q=NullReferenceException")]
    public void Example2TrimsWhiteSpace()
    {
        AssertLink("https://duckduckgo.com/?q=NullReferenceException", SearchWebAction.SearchUrl("  NullReferenceException  "));
    }

    [Fact(DisplayName = "example 3: SearchUrl(\"C# & .NET?\") => https://duckduckgo.com/?q=C%23%20%26%20.NET%3F")]
    public void Example3EncodesReservedCharacters()
    {
        AssertLink("https://duckduckgo.com/?q=C%23%20%26%20.NET%3F", SearchWebAction.SearchUrl("C# & .NET?"));
    }

    [Fact(DisplayName = "example 4: SearchUrl(\"æble\") => https://duckduckgo.com/?q=%C3%A6ble")]
    public void Example4EncodesNonAscii()
    {
        AssertLink("https://duckduckgo.com/?q=%C3%A6ble", SearchWebAction.SearchUrl("æble"));
    }

    [Fact(DisplayName = "example 5: SearchUrl(\"two\\nlines\") => nothing")]
    public void Example5LineBreakGivesNothing()
    {
        Assert.Null(SearchWebAction.SearchUrl("two\nlines"));
    }

    [Fact(DisplayName = "example 6: SearchUrl(\"   \") => nothing")]
    public void Example6WhiteSpaceGivesNothing()
    {
        Assert.Null(SearchWebAction.SearchUrl("   "));
    }

    [Fact(DisplayName = "example 7: SearchUrl(\"!!!\") => nothing; no letter or digit")]
    public void Example7NoLetterOrDigitGivesNothing()
    {
        Assert.Null(SearchWebAction.SearchUrl("!!!"));
    }

    [Fact(DisplayName = "example 8: SearchUrl(\"https://example.com/page\") => nothing; Open link opens it")]
    public void Example8LinkGivesNothing()
    {
        Assert.Null(SearchWebAction.SearchUrl("https://example.com/page"));
    }

    [Fact(DisplayName = "example 9: SearchUrl(a text of 201 characters) => nothing")]
    public void Example9TooLongGivesNothing()
    {
        Assert.Null(SearchWebAction.SearchUrl(new string('a', 201)));
    }

    [Fact(DisplayName = "example 10: CanExecute(text \"hello world\") => true;  GetDescription => \"Searches DuckDuckGo for this text in your browser.\"")]
    public void Example10CanExecuteTextAndDescription()
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en-US");
            CultureInfo.CurrentCulture = new CultureInfo("en-US");

            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello world" });
            var action = new SearchWebAction();

            Assert.True(action.CanExecute(item));
            Assert.Equal("Searches DuckDuckGo for this text in your browser.", action.GetDescription(item));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "example 11: CanExecute(PNG image) => false")]
    public void Example11CannotExecuteImage()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });
        var action = new SearchWebAction();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 12: ExecuteAsync(text \"hello world\") => opens https://duckduckgo.com/?q=hello%20world; nothing is pasted, the clipboard and the history are unchanged")]
    public async Task Example12ExecuteOpensSearch()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello world" });
        var historyBefore = harness.History.Items.Count;
        var writesBefore = harness.Backend.Writes.Count;
        var action = new SearchWebAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var opened = Assert.Single(harness.Launcher.OpenedUrls);
        AssertLink("https://duckduckgo.com/?q=hello%20world", opened);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Equal(writesBefore, harness.Backend.Writes.Count);
        Assert.Equal(historyBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
