// ail-derived-from: ReformatJson@ded044b306b33a89
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the ReformatJson node's examples.</summary>
public sealed class ReformatJsonTests
{
    /// <summary>Formats a nested object with an array.</summary>
    [Fact(DisplayName = @"example 1: Format(""{\""a\"":1,\""b\"":[true,null,\""x\""]}"") => ""{\n  \""a\"": 1,\n  \""b\"": [\n    true,\n    null,\n    \""x\""\n  ]\n}""")]
    public void Example01FormatNestedObject()
    {
        Assert.Equal(
            "{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null,\n    \"x\"\n  ]\n}",
            ReformatJsonAction.Format("{\"a\":1,\"b\":[true,null,\"x\"]}"));
    }

    /// <summary>Keeps empty objects and arrays as they are.</summary>
    [Fact(DisplayName = @"example 2: Format(""{\""empty\"":{},\""none\"":[]}"") => ""{\n  \""empty\"": {},\n  \""none\"": []\n}""")]
    public void Example02FormatEmptyContainers()
    {
        Assert.Equal(
            "{\n  \"empty\": {},\n  \"none\": []\n}",
            ReformatJsonAction.Format("{\"empty\":{},\"none\":[]}"));
    }

    /// <summary>Keeps numbers as written.</summary>
    [Fact(DisplayName = @"example 3: Format(""[1.50,2e3]"") => ""[\n  1.50,\n  2e3\n]""; numbers stay as written")]
    public void Example03FormatKeepsNumbers()
    {
        Assert.Equal("[\n  1.50,\n  2e3\n]", ReformatJsonAction.Format("[1.50,2e3]"));
    }

    /// <summary>Writes non-required characters as themselves.</summary>
    [Fact(DisplayName = @"example 4: Format(""{\""name\"":\""Søren & <co>\""}"") => ""{\n  \""name\"": \""Søren & <co>\""\n}""")]
    public void Example04FormatKeepsCharacters()
    {
        Assert.Equal(
            "{\n  \"name\": \"Søren & <co>\"\n}",
            ReformatJsonAction.Format("{\"name\":\"Søren & <co>\"}"));
    }

    /// <summary>Keeps repeated member names.</summary>
    [Fact(DisplayName = @"example 5: Format(""{\""a\"":1,\""a\"":2}"") => ""{\n  \""a\"": 1,\n  \""a\"": 2\n}""")]
    public void Example05FormatKeepsRepeatedNames()
    {
        Assert.Equal(
            "{\n  \"a\": 1,\n  \"a\": 2\n}",
            ReformatJsonAction.Format("{\"a\":1,\"a\":2}"));
    }

    /// <summary>Minifies an indented object.</summary>
    [Fact(DisplayName = @"example 6: Minify(""{\n  \""a\"": 1,\n  \""b\"": [ true, \""x y\"" ]\n}"") => ""{\""a\"":1,\""b\"":[true,\""x y\""]}""")]
    public void Example06MinifyIndentedObject()
    {
        Assert.Equal(
            "{\"a\":1,\"b\":[true,\"x y\"]}",
            ReformatJsonAction.Minify("{\n  \"a\": 1,\n  \"b\": [ true, \"x y\" ]\n}"));
    }

    /// <summary>Keeps the required escape for a line feed.</summary>
    [Fact(DisplayName = @"example 7: Minify(""[\""line\\nbreak\""]"") => ""[\""line\\nbreak\""]""")]
    public void Example07MinifyKeepsEscape()
    {
        Assert.Equal("[\"line\\nbreak\"]", ReformatJsonAction.Minify("[\"line\\nbreak\"]"));
    }

    /// <summary>Offers formatting for JSON on one line.</summary>
    [Fact(DisplayName = @"example 8: CanExecute(text ""{\""a\"":1}"") => true;  GetTitle => ""Paste formatted JSON""")]
    public void Example08OneLineObjectOffersFormat()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\"a\":1}" });
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionFormatJson, action.GetTitle(item));
    }

    /// <summary>White space around the text does not count.</summary>
    [Fact(DisplayName = @"example 9: CanExecute(text ""  [1, 2]\n"") => true;  GetTitle => ""Paste formatted JSON""; white space around the text does not count")]
    public void Example09SurroundingWhiteSpaceIgnored()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "  [1, 2]\n" });
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionFormatJson, action.GetTitle(item));
    }

    /// <summary>Offers minifying for JSON over several lines.</summary>
    [Fact(DisplayName = @"example 10: CanExecute(text ""{\n  \""a\"": 1\n}"") => true;  GetTitle => ""Paste minified JSON""")]
    public void Example10MultiLineObjectOffersMinify()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\n  \"a\": 1\n}" });
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionMinifyJson, action.GetTitle(item));
    }

    /// <summary>A lone number is not offered.</summary>
    [Fact(DisplayName = @"example 11: CanExecute(text ""42"") => false; only an object or an array")]
    public void Example11NumberNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "42" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>A lone string is not offered.</summary>
    [Fact(DisplayName = @"example 12: CanExecute(text ""\""just a string\"""") => false")]
    public void Example12StringNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "\"just a string\"" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Unquoted names are not strict JSON.</summary>
    [Fact(DisplayName = @"example 13: CanExecute(text ""{a: 1}"") => false; names must be quoted")]
    public void Example13UnquotedNameNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{a: 1}" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>A trailing comma is not strict JSON.</summary>
    [Fact(DisplayName = @"example 14: CanExecute(text ""{\""a\"":1,}"") => false; a trailing comma")]
    public void Example14TrailingCommaNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\"a\":1,}" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>A comment is not strict JSON.</summary>
    [Fact(DisplayName = @"example 15: CanExecute(text ""{\""a\"":1} // note"") => false; a comment")]
    public void Example15CommentNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\"a\":1} // note" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Two values are not one JSON value.</summary>
    [Fact(DisplayName = @"example 16: CanExecute(text ""{\""a\"":1}{\""b\"":2}"") => false; two values")]
    public void Example16TwoValuesNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\"a\":1}{\"b\":2}" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Plain text is not offered.</summary>
    [Fact(DisplayName = @"example 17: CanExecute(text ""hello"") => false")]
    public void Example17PlainTextNotOffered()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>An image is not offered.</summary>
    [Fact(DisplayName = "example 18: CanExecute(PNG image) => false")]
    public void Example18ImageNotOffered()
    {
        using var harness = new ActionTestHarness();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = png });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Pastes JSON on one line formatted, without adding to the history.</summary>
    [Fact(DisplayName = @"example 19: ExecuteAsync(text ""{\""a\"":1}"") => pastes { Text: ""{\n  \""a\"": 1\n}"" }; the history is unchanged")]
    public async Task Example19ExecutePastesFormatted()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\"a\":1}" });
        var historyCount = harness.History.Items.Count;

        await new ReformatJsonAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("{\n  \"a\": 1\n}", written.Text);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }

    /// <summary>Pastes JSON over several lines minified.</summary>
    [Fact(DisplayName = @"example 20: ExecuteAsync(text ""{\n  \""a\"": 1\n}"") => pastes { Text: ""{\""a\"":1}"" }")]
    public async Task Example20ExecutePastesMinified()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "{\n  \"a\": 1\n}" });

        await new ReformatJsonAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("{\"a\":1}", written.Text);
    }
}
