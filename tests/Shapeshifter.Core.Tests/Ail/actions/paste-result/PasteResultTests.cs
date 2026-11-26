// ail-derived-from: PasteResult@98dda2552e49d976
using System.Threading.Tasks;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteResult node: the examples of PasteResultAction.</summary>
public sealed class PasteResultTests
{
    [Fact(DisplayName = "example 1: Calculate(\"12*7+3\") => \"87\"")]
    public void Example01() => Assert.Equal("87", PasteResultAction.Calculate("12*7+3"));

    [Fact(DisplayName = "example 2: Calculate(\" 2 ^ 10 \") => \"1024\"")]
    public void Example02() => Assert.Equal("1024", PasteResultAction.Calculate(" 2 ^ 10 "));

    [Fact(DisplayName = "example 3: Calculate(\"(1+2)/4\") => \"0.75\"")]
    public void Example03() => Assert.Equal("0.75", PasteResultAction.Calculate("(1+2)/4"));

    [Fact(DisplayName = "example 4: Calculate(\"10 / 3\") => \"3.3333333333\"")]
    public void Example04() => Assert.Equal("3.3333333333", PasteResultAction.Calculate("10 / 3"));

    [Fact(DisplayName = "example 5: Calculate(\"0.1 + 0.2\") => \"0.3\"")]
    public void Example05() => Assert.Equal("0.3", PasteResultAction.Calculate("0.1 + 0.2"));

    [Fact(DisplayName = "example 6: Calculate(\"7 % 3\") => \"1\"")]
    public void Example06() => Assert.Equal("1", PasteResultAction.Calculate("7 % 3"));

    [Fact(DisplayName = "example 7: Calculate(\"6 × 7\") => \"42\"")]
    public void Example07() => Assert.Equal("42", PasteResultAction.Calculate("6 × 7"));

    [Fact(DisplayName = "example 8: Calculate(\"10 ÷ 4\") => \"2.5\"")]
    public void Example08() => Assert.Equal("2.5", PasteResultAction.Calculate("10 ÷ 4"));

    [Fact(DisplayName = "example 9: Calculate(\"-3 * -2\") => \"6\"")]
    public void Example09() => Assert.Equal("6", PasteResultAction.Calculate("-3 * -2"));

    [Fact(DisplayName = "example 10: Calculate(\"10 - 3\") => \"7\"")]
    public void Example10() => Assert.Equal("7", PasteResultAction.Calculate("10 - 3"));

    [Fact(DisplayName = "example 11: Calculate(\"10-3\") => nothing")]
    public void Example11() => Assert.Null(PasteResultAction.Calculate("10-3"));

    [Fact(DisplayName = "example 12: Calculate(\"2026-11-26\") => nothing")]
    public void Example12() => Assert.Null(PasteResultAction.Calculate("2026-11-26"));

    [Fact(DisplayName = "example 13: Calculate(\"12/05\") => nothing")]
    public void Example13() => Assert.Null(PasteResultAction.Calculate("12/05"));

    [Fact(DisplayName = "example 14: Calculate(\"1.2.3\") => nothing")]
    public void Example14() => Assert.Null(PasteResultAction.Calculate("1.2.3"));

    [Fact(DisplayName = "example 15: Calculate(\"42\") => nothing")]
    public void Example15() => Assert.Null(PasteResultAction.Calculate("42"));

    [Fact(DisplayName = "example 16: Calculate(\"1 / 0\") => nothing")]
    public void Example16() => Assert.Null(PasteResultAction.Calculate("1 / 0"));

    [Fact(DisplayName = "example 17: Calculate(\"99999999999 * 99999999999\") => nothing")]
    public void Example17() => Assert.Null(PasteResultAction.Calculate("99999999999 * 99999999999"));

    [Fact(DisplayName = "example 18: Calculate(\"2 ^ 0.5\") => \"1.4142135624\"")]
    public void Example18() => Assert.Equal("1.4142135624", PasteResultAction.Calculate("2 ^ 0.5"));

    [Fact(DisplayName = "example 19: Calculate(\"2 + x\") => nothing")]
    public void Example19() => Assert.Null(PasteResultAction.Calculate("2 + x"));

    [Fact(DisplayName = "example 20: Calculate(\"(1 + 2\") => nothing")]
    public void Example20() => Assert.Null(PasteResultAction.Calculate("(1 + 2"));

    [Fact(DisplayName = "example 21: Calculate(\"Sqrt(4)\") => nothing")]
    public void Example21() => Assert.Null(PasteResultAction.Calculate("Sqrt(4)"));

    [Fact(DisplayName = "example 22: CanExecute(text \"12*7+3\") => true;  GetDescription => \"Calculates this and pastes 87.\"")]
    public void Example22()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "12*7+3" });
        var action = new PasteResultAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal("Calculates this and pastes 87.", action.GetDescription(item));
    }

    [Fact(DisplayName = "example 23: ExecuteAsync(text \"12*7+3\") => pastes { Text: \"87\" }; the history is unchanged")]
    public async Task Example23()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "12*7+3" });
        var countBefore = harness.History.Items.Count;
        var action = new PasteResultAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("87", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(countBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
