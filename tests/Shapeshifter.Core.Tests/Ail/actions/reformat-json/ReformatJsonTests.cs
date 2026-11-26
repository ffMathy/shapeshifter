// ail-derived-from: ReformatJson@52feb7edaac82459
using System.Threading.Tasks;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the ReformatJson node's examples.</summary>
public sealed class ReformatJsonTests
{
    private static ClipboardItem TextItem(ActionTestHarness harness, string text) =>
        harness.AddToHistory(new ClipboardSnapshot { Text = text });

    /// <summary>Example 1.</summary>
    [Fact(DisplayName = @"example 1: Format(""{\""a\"":1,\""b\"":[true,null,\""x\""]}"") => ""{\n  \""a\"": 1,\n  \""b\"": [\n    true,\n    null,\n    \""x\""\n  ]\n}""")]
    public void Example01FormatsNestedValues()
    {
        Assert.Equal(
            "{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null,\n    \"x\"\n  ]\n}",
            ReformatJsonAction.Format("{\"a\":1,\"b\":[true,null,\"x\"]}"));
    }

    /// <summary>Example 2.</summary>
    [Fact(DisplayName = @"example 2: Format(""{\""empty\"":{},\""none\"":[]}"") => ""{\n  \""empty\"": {},\n  \""none\"": []\n}""")]
    public void Example02KeepsEmptyObjectsAndArrays()
    {
        Assert.Equal(
            "{\n  \"empty\": {},\n  \"none\": []\n}",
            ReformatJsonAction.Format("{\"empty\":{},\"none\":[]}"));
    }

    /// <summary>Example 3.</summary>
    [Fact(DisplayName = @"example 3: Format(""[1.50,2e3]"") => ""[\n  1.50,\n  2e3\n]""; numbers stay as written")]
    public void Example03KeepsNumbersAsWritten()
    {
        Assert.Equal("[\n  1.50,\n  2e3\n]", ReformatJsonAction.Format("[1.50,2e3]"));
    }

    /// <summary>Example 4.</summary>
    [Fact(DisplayName = @"example 4: Format(""{\""name\"":\""Søren & <co>\""}"") => ""{\n  \""name\"": \""Søren & <co>\""\n}""")]
    public void Example04WritesCharactersAsThemselves()
    {
        Assert.Equal(
            "{\n  \"name\": \"Søren & <co>\"\n}",
            ReformatJsonAction.Format("{\"name\":\"Søren & <co>\"}"));
    }

    /// <summary>Example 5.</summary>
    [Fact(DisplayName = @"example 5: Format(""{\""a\"":1,\""a\"":2}"") => ""{\n  \""a\"": 1,\n  \""a\"": 2\n}""")]
    public void Example05KeepsRepeatedNames()
    {
        Assert.Equal(
            "{\n  \"a\": 1,\n  \"a\": 2\n}",
            ReformatJsonAction.Format("{\"a\":1,\"a\":2}"));
    }

    /// <summary>Example 6.</summary>
    [Fact(DisplayName = @"example 6: Minify(""{\n  \""a\"": 1,\n  \""b\"": [ true, \""x y\"" ]\n}"") => ""{\""a\"":1,\""b\"":[true,\""x y\""]}""")]
    public void Example06MinifiesWhiteSpaceBetweenValues()
    {
        Assert.Equal(
            "{\"a\":1,\"b\":[true,\"x y\"]}",
            ReformatJsonAction.Minify("{\n  \"a\": 1,\n  \"b\": [ true, \"x y\" ]\n}"));
    }

    /// <summary>Example 7.</summary>
    [Fact(DisplayName = @"example 7: Minify(""[\""line\\nbreak\""]"") => ""[\""line\\nbreak\""]""")]
    public void Example07KeepsRequiredEscapes()
    {
        Assert.Equal("[\"line\\nbreak\"]", ReformatJsonAction.Minify("[\"line\\nbreak\"]"));
    }

    /// <summary>Example 8.</summary>
    [Fact(DisplayName = @"example 8: CanExecute(text ""{\""a\"":1}"") => true;  GetTitle => ""Paste formatted JSON""")]
    public void Example08OneLineObjectOffersFormat()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\"a\":1}");
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionFormatJson, action.GetTitle(item));
    }

    /// <summary>Example 9.</summary>
    [Fact(DisplayName = @"example 9: CanExecute(text ""  [1, 2]\n"") => true;  GetTitle => ""Paste formatted JSON""; white space around the text does not count")]
    public void Example09SurroundingWhiteSpaceDoesNotCount()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "  [1, 2]\n");
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionFormatJson, action.GetTitle(item));
    }

    /// <summary>Example 10.</summary>
    [Fact(DisplayName = @"example 10: CanExecute(text ""{\n  \""a\"": 1\n}"") => true;  GetTitle => ""Paste minified JSON""")]
    public void Example10MultiLineObjectOffersMinify()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\n  \"a\": 1\n}");
        var action = new ReformatJsonAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal(Strings.ActionMinifyJson, action.GetTitle(item));
    }

    /// <summary>Example 11.</summary>
    [Fact(DisplayName = @"example 11: CanExecute(text ""42"") => false; only an object or an array")]
    public void Example11RejectsLoneNumber()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "42");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 12.</summary>
    [Fact(DisplayName = @"example 12: CanExecute(text ""\""just a string\"""") => false")]
    public void Example12RejectsLoneString()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "\"just a string\"");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 13.</summary>
    [Fact(DisplayName = @"example 13: CanExecute(text ""{a: 1}"") => false; names must be quoted")]
    public void Example13RejectsUnquotedNames()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{a: 1}");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 14.</summary>
    [Fact(DisplayName = @"example 14: CanExecute(text ""{\""a\"":1,}"") => false; a trailing comma")]
    public void Example14RejectsTrailingComma()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\"a\":1,}");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 15.</summary>
    [Fact(DisplayName = @"example 15: CanExecute(text ""{\""a\"":1} // note"") => false; a comment")]
    public void Example15RejectsComment()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\"a\":1} // note");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 16.</summary>
    [Fact(DisplayName = @"example 16: CanExecute(text ""{\""a\"":1}{\""b\"":2}"") => false; two values")]
    public void Example16RejectsTwoValues()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\"a\":1}{\"b\":2}");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 17.</summary>
    [Fact(DisplayName = @"example 17: CanExecute(text ""hello"") => false")]
    public void Example17RejectsPlainText()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "hello");

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 18.</summary>
    [Fact(DisplayName = @"example 18: CanExecute(PNG image) => false")]
    public void Example18RejectsImage()
    {
        using var harness = new ActionTestHarness();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = png });

        Assert.False(new ReformatJsonAction().CanExecute(item));
    }

    /// <summary>Example 19.</summary>
    [Fact(DisplayName = @"example 19: ExecuteAsync(text ""{\""a\"":1}"") => pastes { Text: ""{\n  \""a\"": 1\n}"" }; the history is unchanged")]
    public async Task Example19PastesFormattedJsonWithoutAddingToHistory()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\"a\":1}");
        var historyCount = harness.History.Items.Count;

        await new ReformatJsonAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("{\n  \"a\": 1\n}", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }

    /// <summary>Example 20.</summary>
    [Fact(DisplayName = @"example 20: ExecuteAsync(text ""{\n  \""a\"": 1\n}"") => pastes { Text: ""{\""a\"":1}"" }")]
    public async Task Example20PastesMinifiedJson()
    {
        using var harness = new ActionTestHarness();
        var item = TextItem(harness, "{\n  \"a\": 1\n}");

        await new ReformatJsonAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("{\"a\":1}", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
    }
}
