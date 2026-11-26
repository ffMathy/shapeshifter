// ail-derived-from: PasteAsMarkdown@02ba26c55e22dd1e
using System.Text;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteAsMarkdown node's examples.</summary>
public sealed class PasteAsMarkdownTests
{
    private static RawClipboardFormat Html(string html) =>
        new("text/html", Encoding.UTF8.GetBytes(html));

    [Fact(DisplayName = "example 1: Convert(\"<h1>Title</h1><p>Some <b>bold</b> and <i>italic</i> and a <a href=\\\"https://example.com\\\">link</a>.</p>\") => \"# Title\\n\\nSome **bold** and *italic* and a [link](https://example.com).\"")]
    public void Example01()
    {
        var result = PasteAsMarkdownAction.Convert(
            "<h1>Title</h1><p>Some <b>bold</b> and <i>italic</i> and a <a href=\"https://example.com\">link</a>.</p>");
        Assert.Equal("# Title\n\nSome **bold** and *italic* and a [link](https://example.com).", result);
    }

    [Fact(DisplayName = "example 2: Convert(\"<ul><li>one</li><li>two</li></ul>\") => \"- one\\n- two\"")]
    public void Example02()
    {
        var result = PasteAsMarkdownAction.Convert("<ul><li>one</li><li>two</li></ul>");
        Assert.Equal("- one\n- two", result);
    }

    [Fact(DisplayName = "example 3: Convert(\"<ol><li>a</li><li>b</li></ol>\") => \"1. a\\n2. b\"")]
    public void Example03()
    {
        var result = PasteAsMarkdownAction.Convert("<ol><li>a</li><li>b</li></ol>");
        Assert.Equal("1. a\n2. b", result);
    }

    [Fact(DisplayName = "example 4: Convert(\"<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>\") => \"| A | B |\\n| --- | --- |\\n| 1 | 2 |\"")]
    public void Example04()
    {
        var result = PasteAsMarkdownAction.Convert(
            "<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>");
        Assert.Equal("| A | B |\n| --- | --- |\n| 1 | 2 |", result);
    }

    [Fact(DisplayName = "example 5: Convert(\"<html><body><!--StartFragment--><p>Hi <code>code</code></p><!--EndFragment--></body></html>\") => \"Hi `code`\"")]
    public void Example05()
    {
        var result = PasteAsMarkdownAction.Convert(
            "<html><body><!--StartFragment--><p>Hi <code>code</code></p><!--EndFragment--></body></html>");
        Assert.Equal("Hi `code`", result);
    }

    [Fact(DisplayName = "example 6: Convert(\"<p>Hi <a href=\\\"https://e.com\\\">https://e.com</a> <blink>odd</blink></p>\") => \"Hi https://e.com odd\"")]
    public void Example06()
    {
        var result = PasteAsMarkdownAction.Convert(
            "<p>Hi <a href=\"https://e.com\">https://e.com</a> <blink>odd</blink></p>");
        Assert.Equal("Hi https://e.com odd", result);
    }

    [Fact(DisplayName = "example 7: CanExecute(text \"Hello World\" + raw \"text/html\" \"<b>Hello</b> World\") => true")]
    public void Example07()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot
        {
            Text = "Hello World",
            RawFormats = [Html("<b>Hello</b> World")],
        });

        Assert.True(new PasteAsMarkdownAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 8: CanExecute(text \"hello world\") => false")]
    public void Example08()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello world" });

        Assert.False(new PasteAsMarkdownAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 9: CanExecute(raw \"text/html\" \"<p> </p>\" only) => false")]
    public void Example09()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot
        {
            RawFormats = [Html("<p> </p>")],
        });

        Assert.False(new PasteAsMarkdownAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 10: CanExecute(PNG image) => false")]
    public void Example10()
    {
        using var harness = new ActionTestHarness();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = png });

        Assert.False(new PasteAsMarkdownAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 11: ExecuteAsync(text \"Hello World\" + raw \"text/html\" \"<b>Hello</b> World\") => pastes { Text: \"**Hello** World\" }")]
    public async Task Example11()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot
        {
            Text = "Hello World",
            RawFormats = [Html("<b>Hello</b> World")],
        });
        var countBefore = harness.History.Items.Count;

        await new PasteAsMarkdownAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var write = Assert.Single(harness.Backend.Writes);
        Assert.Equal("**Hello** World", write.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(countBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
