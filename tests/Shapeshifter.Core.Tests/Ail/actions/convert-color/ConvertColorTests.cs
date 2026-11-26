// ail-derived-from: ConvertColor@0bb0b5ecd3a460f5
using System.Globalization;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the ConvertColor node: parsing, formatting and the three colour actions.</summary>
public sealed class ConvertColorTests
{
    private static ParsedColor ParseColor(string text)
    {
        var result = ConvertColor.Parse(text);
        return result is { } color
            ? color
            : throw new Xunit.Sdk.XunitException($"Expected \"{text}\" to parse as a colour.");
    }

    private static void AssertRgb(ParsedColor color, int red, int green, int blue)
    {
        Assert.Equal(red, color.Red);
        Assert.Equal(green, color.Green);
        Assert.Equal(blue, color.Blue);
    }

    private static void WithNeutralCulture(Action body)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
        try
        {
            body();
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Fact(DisplayName = "example 1: Parse(\"#3584e4\") => { Red: 53, Green: 132, Blue: 228, Alpha: 1, Notation: \"hex\" }")]
    public void Example01()
    {
        var color = ParseColor("#3584e4");
        AssertRgb(color, 53, 132, 228);
        Assert.Equal(1.0, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Hex, color.Notation);
    }

    [Fact(DisplayName = "example 2: Parse(\" #FFF \") => { Red: 255, Green: 255, Blue: 255, Alpha: 1, Notation: \"hex\" }")]
    public void Example02()
    {
        var color = ParseColor(" #FFF ");
        AssertRgb(color, 255, 255, 255);
        Assert.Equal(1.0, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Hex, color.Notation);
    }

    [Fact(DisplayName = "example 3: Parse(\"#3584e480\") => Alpha 128/255, about 0.5")]
    public void Example03()
    {
        var color = ParseColor("#3584e480");
        Assert.Equal(128 / 255.0, (double)color.Alpha, 3);
    }

    [Fact(DisplayName = "example 4: Parse(\"rgb(53, 132, 228)\") => { Red: 53, Green: 132, Blue: 228, Alpha: 1, Notation: \"rgb\" }")]
    public void Example04()
    {
        var color = ParseColor("rgb(53, 132, 228)");
        AssertRgb(color, 53, 132, 228);
        Assert.Equal(1.0, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Rgb, color.Notation);
    }

    [Fact(DisplayName = "example 5: Parse(\"RGBA(53,132,228,0.5)\") => Alpha 0.5, Notation \"rgb\"")]
    public void Example05()
    {
        var color = ParseColor("RGBA(53,132,228,0.5)");
        Assert.Equal(0.5, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Rgb, color.Notation);
    }

    [Fact(DisplayName = "example 6: Parse(\"rgb(53 132 228 / 50%)\") => Alpha 0.5, Notation \"rgb\"")]
    public void Example06()
    {
        var color = ParseColor("rgb(53 132 228 / 50%)");
        Assert.Equal(0.5, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Rgb, color.Notation);
    }

    [Fact(DisplayName = "example 7: Parse(\"rgb(100%, 0%, 50%)\") => { Red: 255, Green: 0, Blue: 128 }")]
    public void Example07()
    {
        var color = ParseColor("rgb(100%, 0%, 50%)");
        AssertRgb(color, 255, 0, 128);
    }

    [Fact(DisplayName = "example 8: Parse(\"hsl(210, 50%, 40%)\") => { Red: 51, Green: 102, Blue: 153, Alpha: 1, Notation: \"hsl\" }")]
    public void Example08()
    {
        var color = ParseColor("hsl(210, 50%, 40%)");
        AssertRgb(color, 51, 102, 153);
        Assert.Equal(1.0, (double)color.Alpha, 6);
        Assert.Equal(ColorNotation.Hsl, color.Notation);
    }

    [Fact(DisplayName = "example 9: Parse(\"hsl(120deg 100% 25%)\") => { Red: 0, Green: 128, Blue: 0 }")]
    public void Example09()
    {
        var color = ParseColor("hsl(120deg 100% 25%)");
        AssertRgb(color, 0, 128, 0);
    }

    [Fact(DisplayName = "example 10: Parse(\"rgb(256, 0, 0)\") => nothing; out of range")]
    public void Example10()
    {
        Assert.False(ConvertColor.Parse("rgb(256, 0, 0)") is { });
    }

    [Fact(DisplayName = "example 11: Parse(\"#12345\") => nothing; five digits")]
    public void Example11()
    {
        Assert.False(ConvertColor.Parse("#12345") is { });
    }

    [Fact(DisplayName = "example 12: Parse(\"the colour #3584e4\") => nothing; only a colour on its own")]
    public void Example12()
    {
        Assert.False(ConvertColor.Parse("the colour #3584e4") is { });
    }

    [Fact(DisplayName = "example 13: Parse(\"red\") => nothing; colour names are not read")]
    public void Example13()
    {
        Assert.False(ConvertColor.Parse("red") is { });
    }

    [Fact(DisplayName = "example 14: Format({ 53, 132, 228, 1 }, \"rgb\") => \"rgb(53, 132, 228)\"")]
    public void Example14()
    {
        var color = ParseColor("#3584e4");
        Assert.Equal("rgb(53, 132, 228)", ConvertColor.Format(color, ColorNotation.Rgb));
    }

    [Fact(DisplayName = "example 15: Format({ 53, 132, 228, 1 }, \"hsl\") => \"hsl(213, 76%, 55%)\"")]
    public void Example15()
    {
        var color = ParseColor("rgb(53, 132, 228)");
        Assert.Equal("hsl(213, 76%, 55%)", ConvertColor.Format(color, ColorNotation.Hsl));
    }

    [Fact(DisplayName = "example 16: Format({ 53, 132, 228, 1 }, \"hex\") => \"#3584e4\"")]
    public void Example16()
    {
        var color = ParseColor("rgb(53, 132, 228)");
        Assert.Equal("#3584e4", ConvertColor.Format(color, ColorNotation.Hex));
    }

    [Fact(DisplayName = "example 17: Format({ 53, 132, 228, 0.5 }, \"rgb\") => \"rgba(53, 132, 228, 0.5)\"")]
    public void Example17()
    {
        var color = ParseColor("hsla(213, 76%, 55%, 0.5)");
        var exact = ParseColor("rgba(53, 132, 228, 0.5)");
        Assert.Equal("rgba(53, 132, 228, 0.5)", ConvertColor.Format(exact, ColorNotation.Rgb));
        Assert.Equal(0.5, (double)color.Alpha, 6);
    }

    [Fact(DisplayName = "example 18: Format({ 53, 132, 228, 0.5 }, \"hex\") => \"#3584e480\"")]
    public void Example18()
    {
        var color = ParseColor("rgba(53, 132, 228, 0.5)");
        Assert.Equal("#3584e480", ConvertColor.Format(color, ColorNotation.Hex));
    }

    [Fact(DisplayName = "example 19: Format({ 255, 0, 0, 0.25 }, \"hsl\") => \"hsla(0, 100%, 50%, 0.25)\"")]
    public void Example19()
    {
        var color = ParseColor("rgba(255, 0, 0, 0.25)");
        Assert.Equal("hsla(0, 100%, 50%, 0.25)", ConvertColor.Format(color, ColorNotation.Hsl));
    }

    [Fact(DisplayName = "example 20: Format({ 128, 128, 128, 1 }, \"hsl\") => \"hsl(0, 0%, 50%)\"")]
    public void Example20()
    {
        var color = ParseColor("rgb(128, 128, 128)");
        Assert.Equal("hsl(0, 0%, 50%)", ConvertColor.Format(color, ColorNotation.Hsl));
    }

    [Fact(DisplayName = "example 21: the actions of text \"#3584e4\" => \"Paste as RGB colour\" described \"Pastes rgb(53, 132, 228).\", and \"Paste as HSL colour\" described \"Pastes hsl(213, 76%, 55%).\"")]
    public void Example21()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "#3584e4" });
        var hex = new PasteAsHexColorAction();
        var rgb = new PasteAsRgbColorAction();
        var hsl = new PasteAsHslColorAction();

        WithNeutralCulture(() =>
        {
            Assert.False(hex.CanExecute(item));
            Assert.True(rgb.CanExecute(item));
            Assert.True(hsl.CanExecute(item));
            Assert.Equal("Paste as RGB colour", rgb.GetTitle(item));
            Assert.Equal("Pastes rgb(53, 132, 228).", rgb.GetDescription(item));
            Assert.Equal("Paste as HSL colour", hsl.GetTitle(item));
            Assert.Equal("Pastes hsl(213, 76%, 55%).", hsl.GetDescription(item));
        });
    }

    [Fact(DisplayName = "example 22: the actions of text \"hsl(210, 50%, 40%)\" => \"Paste as hex colour\" (\"Pastes #336699.\"), and \"Paste as RGB colour\" (\"Pastes rgb(51, 102, 153).\")")]
    public void Example22()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hsl(210, 50%, 40%)" });
        var hex = new PasteAsHexColorAction();
        var rgb = new PasteAsRgbColorAction();
        var hsl = new PasteAsHslColorAction();

        WithNeutralCulture(() =>
        {
            Assert.True(hex.CanExecute(item));
            Assert.True(rgb.CanExecute(item));
            Assert.False(hsl.CanExecute(item));
            Assert.Equal("Paste as hex colour", hex.GetTitle(item));
            Assert.Equal("Pastes #336699.", hex.GetDescription(item));
            Assert.Equal("Paste as RGB colour", rgb.GetTitle(item));
            Assert.Equal("Pastes rgb(51, 102, 153).", rgb.GetDescription(item));
        });
    }

    [Fact(DisplayName = "example 23: ExecuteAsync of Paste as RGB colour on text \"#3584e4\" => pastes { Text: \"rgb(53, 132, 228)\" }; the history is unchanged")]
    public async Task Example23()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "#3584e4" });
        var historyBefore = harness.History.Items.Count;
        var action = new PasteAsRgbColorAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("rgb(53, 132, 228)", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Count);
    }
}
