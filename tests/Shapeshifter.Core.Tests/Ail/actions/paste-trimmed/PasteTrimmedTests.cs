// ail-derived-from: PasteTrimmed@220e174d7e51fce3
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteTrimmed node: its examples, translated once.</summary>
public sealed class PasteTrimmedTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    /// <summary>Example 1: text with a leading space and a trailing line break is offered.</summary>
    [Fact(DisplayName = "example 1: CanExecute(text \"  hello\\n\") => true")]
    public void Example1LeadingSpacesAndTrailingLineBreakIsOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "  hello\n" });

        Assert.True(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 2: text without surrounding white space is not offered.</summary>
    [Fact(DisplayName = "example 2: CanExecute(text \"hello\") => false")]
    public void Example2TextWithoutSurroundingWhiteSpaceIsNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        Assert.False(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 3: white space inside the text does not count.</summary>
    [Fact(DisplayName = "example 3: CanExecute(text \"a b\") => false; white space inside the text does not count")]
    public void Example3InnerWhiteSpaceDoesNotCount()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "a b" });

        Assert.False(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 4: text that is only white space is not offered.</summary>
    [Fact(DisplayName = "example 4: CanExecute(text \"   \") => false")]
    public void Example4OnlyWhiteSpaceIsNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "   " });

        Assert.False(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 5: a leading non-breaking space is white space.</summary>
    [Fact(DisplayName = "example 5: CanExecute(text \"\u00A0hello\") => true; a non-breaking space is white space")]
    public void Example5NonBreakingSpaceIsWhiteSpace()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "\u00A0hello" });

        Assert.True(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 6: an image is not offered.</summary>
    [Fact(DisplayName = "example 6: CanExecute(PNG image) => false")]
    public void Example6ImageIsNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });

        Assert.False(new PasteTrimmedAction().CanExecute(item));
    }

    /// <summary>Example 7: only the start and the end are trimmed, and the trimmed text is pasted.</summary>
    [Fact(DisplayName = "example 7: ExecuteAsync(text \"\\n\\n  two\\n  lines \\t\\r\\n\") => pastes { Text: \"two\\n  lines\" }")]
    public async Task Example7PastesTheTrimmedText()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "\n\n  two\n  lines \t\r\n" });

        await new PasteTrimmedAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Backend.Writes);
        var written = harness.Backend.Writes[0];
        Assert.Equal("two\n  lines", written.Text);
        Assert.Null(written.Image);
        Assert.Empty(written.Files);
        Assert.Equal(ClipboardContentKind.Text, written.Kind);
        Assert.Equal(1, harness.Paste.Calls);
    }

    /// <summary>Example 8: pasting trimmed text adds nothing to the history.</summary>
    [Fact(DisplayName = "example 8: ExecuteAsync(text \" x\") => the history is unchanged")]
    public async Task Example8HistoryIsUnchanged()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = " x" });
        var before = harness.History.Items.Select(i => i.Id).ToList();

        await new PasteTrimmedAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var after = harness.History.Items.Select(i => i.Id).ToList();
        Assert.Equal(before, after);
    }
}
