// ail-derived-from: UrlDecode@e29c1f7dda6ab732
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the UrlDecode node: the examples of UrlDecodeAction.</summary>
public sealed class UrlDecodeTests
{
    /// <summary>Example 1: a link with a space and UTF-8 letters.</summary>
    [Fact(DisplayName = "example 1: TryDecode(\"https://example.com/a%20b?q=%C3%A6%C3%B8\") => \"https://example.com/a b?q=æø\"")]
    public void Example1()
    {
        Assert.Equal("https://example.com/a b?q=æø", UrlDecodeAction.TryDecode("https://example.com/a%20b?q=%C3%A6%C3%B8"));
    }

    /// <summary>Example 2: upper- and lower-case hexadecimal digits.</summary>
    [Fact(DisplayName = "example 2: TryDecode(\"name%3Dvalue%26x%3d1\") => \"name=value&x=1\"; lower-case hexadecimal digits work too")]
    public void Example2()
    {
        Assert.Equal("name=value&x=1", UrlDecodeAction.TryDecode("name%3Dvalue%26x%3d1"));
    }

    /// <summary>Example 3: + is left as it is.</summary>
    [Fact(DisplayName = "example 3: TryDecode(\"a+b%21\") => \"a+b!\"; + is not a space")]
    public void Example3()
    {
        Assert.Equal("a+b!", UrlDecodeAction.TryDecode("a+b%21"));
    }

    /// <summary>Example 4: a stray % is not a %-code.</summary>
    [Fact(DisplayName = "example 4: TryDecode(\"50% off\") => nothing; no %-code")]
    public void Example4()
    {
        Assert.Null(UrlDecodeAction.TryDecode("50% off"));
    }

    /// <summary>Example 5: a trailing % is not a %-code.</summary>
    [Fact(DisplayName = "example 5: TryDecode(\"100%\") => nothing")]
    public void Example5()
    {
        Assert.Null(UrlDecodeAction.TryDecode("100%"));
    }

    /// <summary>Example 6: % followed by non-hexadecimal characters.</summary>
    [Fact(DisplayName = "example 6: TryDecode(\"%zz\") => nothing")]
    public void Example6()
    {
        Assert.Null(UrlDecodeAction.TryDecode("%zz"));
    }

    /// <summary>Example 7: an incomplete UTF-8 sequence.</summary>
    [Fact(DisplayName = "example 7: TryDecode(\"%C3\") => nothing; half a character is not UTF-8")]
    public void Example7()
    {
        Assert.Null(UrlDecodeAction.TryDecode("%C3"));
    }

    /// <summary>Example 8: a decoded control character.</summary>
    [Fact(DisplayName = "example 8: TryDecode(\"%00\") => nothing; a control character")]
    public void Example8()
    {
        Assert.Null(UrlDecodeAction.TryDecode("%00"));
    }

    /// <summary>Example 9: an encoded percent sign.</summary>
    [Fact(DisplayName = "example 9: TryDecode(\"100%25 sure\") => \"100% sure\"")]
    public void Example9()
    {
        Assert.Equal("100% sure", UrlDecodeAction.TryDecode("100%25 sure"));
    }

    /// <summary>Example 10: nothing gives nothing.</summary>
    [Fact(DisplayName = "example 10: TryDecode(nothing) => nothing")]
    public void Example10()
    {
        Assert.Null(UrlDecodeAction.TryDecode(null));
    }

    /// <summary>Example 11: offered for a text with a %-code.</summary>
    [Fact(DisplayName = "example 11: CanExecute(text \"a%20b\") => true")]
    public void Example11()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "a%20b" });
        var action = new UrlDecodeAction();

        Assert.True(action.CanExecute(item));
    }

    /// <summary>Example 12: not offered for a text without a %-code.</summary>
    [Fact(DisplayName = "example 12: CanExecute(text \"hello world\") => false")]
    public void Example12()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello world" });
        var action = new UrlDecodeAction();

        Assert.False(action.CanExecute(item));
    }

    /// <summary>Example 13: pastes the decoded text and leaves the history unchanged.</summary>
    [Fact(DisplayName = "example 13: ExecuteAsync(text \"a%20b\") => pastes { Text: \"a b\" }; the history is unchanged")]
    public async Task Example13()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "a%20b" });
        var action = new UrlDecodeAction();
        var historyBefore = harness.History.Items.Select(i => i.Id).ToArray();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Backend.Writes);
        Assert.Equal("a b", harness.Backend.Writes[0].Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Select(i => i.Id).ToArray());
    }
}
