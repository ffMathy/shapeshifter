// ail-derived-from: Picker@f0aabbdff554e176
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the scenarios of the Picker view.</summary>
public sealed class PickerTests
{
    /// <summary>The picker shows its header, history, preview, actions and key hints.</summary>
    [Fact(DisplayName = "the parts of the picker")]
    public Task ThePartsOfThePicker() => AppHarness.RunAsync(async app =>
    {
        const string command = "dotnet run --project src/App";
        await app.CopyAsync("alpha", null);
        await app.CopyAsync("bravo", null);
        await app.CopyAsync(command, null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker);

        Assert.Contains("Shapeshifter", texts);
        Assert.Contains("History", texts);
        Assert.Contains("3 items", texts);

        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);
        Assert.Contains(command, RowTexts(chosen));

        // The text shows once in its row and once more in the preview card.
        Assert.True(Occurrences(texts, command) >= 2);

        Assert.Contains("Actions", texts);
        Assert.True(app.ActionRows.Count > 0);
        var actionsHeading = IndexOf(texts, "Actions");
        foreach (var row in app.ActionRows)
        {
            foreach (var text in RowTexts(row))
            {
                Assert.True(IndexOf(texts, text) > actionsHeading);
            }
        }

        // The footer comes last in reading order and shows key hints of its own.
        var known = new HashSet<string> { "Shapeshifter", "History", "3 items", "Actions" };
        foreach (var row in app.HistoryRows)
        {
            known.UnionWith(RowTexts(row));
        }

        foreach (var row in app.ActionRows)
        {
            known.UnionWith(RowTexts(row));
        }

        Assert.True(texts.Count > 0);
        Assert.DoesNotContain(texts[texts.Count - 1], known);

        AppHarness.SaveScreen(app.Picker, "the-parts-of-the-picker.png");
    });

    /// <summary>A history of one item is counted in the singular.</summary>
    [Fact(DisplayName = "one item is counted as such")]
    public Task OneItemIsCountedAsSuch() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("solo", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.Contains("1 item", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "one-item-is-counted-as-such.png");
    });

    /// <summary>A history longer than fits scrolls the chosen row into view.</summary>
    [Fact(DisplayName = "a long history scrolls")]
    public Task ALongHistoryScrolls() => AppHarness.RunAsync(async app =>
    {
        for (var i = 1; i <= 8; i++)
        {
            await app.CopyAsync($"item {i}", null);
        }

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.Equal(7, FullyShown(app.HistoryRows));
        Assert.Contains("8 items", AppHarness.Texts(app.Picker));

        var rows = app.HistoryRows;
        Assert.Equal(8, rows.Count);
        for (var i = 0; i < 7; i++)
        {
            await app.TapAsync(GestureKey.Down);
        }

        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.Same(app.HistoryRows[7], chosen);
        Assert.True(AppHarness.IsFullyShown(chosen));

        AppHarness.SaveScreen(app.Picker, "a-long-history-scrolls.png");
    }, new AppOptions { ScreenHeight = 800 });

    private static List<string> RowTexts(Control row)
    {
        var texts = new List<string>();
        foreach (var block in row.GetVisualDescendants().OfType<TextBlock>())
        {
            if (!string.IsNullOrEmpty(block.Text))
            {
                texts.Add(block.Text);
            }
        }

        return texts;
    }

    private static int FullyShown(IReadOnlyList<ListBoxItem> rows)
    {
        var count = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            if (AppHarness.IsFullyShown(rows[i]))
            {
                count++;
            }
        }

        return count;
    }

    private static int Occurrences(IReadOnlyList<string> texts, string text)
    {
        var count = 0;
        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i] == text)
            {
                count++;
            }
        }

        return count;
    }

    private static int IndexOf(IReadOnlyList<string> texts, string text)
    {
        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i] == text)
            {
                return i;
            }
        }

        return -1;
    }
}
