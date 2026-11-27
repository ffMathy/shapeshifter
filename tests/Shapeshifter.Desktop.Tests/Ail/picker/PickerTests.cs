// ail-derived-from: Picker@f0aabbdff554e176
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Picker scenarios.</summary>
public sealed class PickerTests
{
    /// <summary>Scenario: the parts of the picker.</summary>
    [Fact(DisplayName = "the parts of the picker")]
    public Task ThePartsOfThePicker() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("alpha", null);
        await app.CopyAsync("bravo", null);
        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker).ToList();

        // The header says "Shapeshifter".
        var title = texts.IndexOf("Shapeshifter");
        Assert.True(title >= 0, "the header shows \"Shapeshifter\"");

        // The history is titled "History" with "3 items" beside it.
        var heading = texts.IndexOf("History");
        Assert.True(heading >= 0, "the history is titled \"History\"");
        var count = texts.IndexOf("3 items");
        Assert.True(count >= 0, "the count says \"3 items\"");
        Assert.True(title < heading, "the header comes before the history");
        Assert.True(heading < count, "the count sits beside the history's title");
        Assert.Equal(3, app.History.Items.Count);

        // The row "dotnet run --project src/App" is chosen.
        Assert.Equal(3, app.HistoryRows.Count);
        Assert.True(app.HistoryRows[0].IsSelected, "the newest row is chosen");
        Assert.Same(app.HistoryRows[0], app.ChosenRow);
        Assert.False(app.HistoryRows[1].IsSelected);
        Assert.False(app.HistoryRows[2].IsSelected);

        // Its preview shows the same text: once in the row, once in the preview.
        var shown = texts.FindAll(t => t == "dotnet run --project src/App").Count;
        Assert.True(shown >= 2, "the chosen text shows in its row and in the preview");

        // Its actions are listed under "Actions".
        var actions = texts.IndexOf("Actions");
        Assert.True(actions >= 0, "the actions are headed \"Actions\"");
        Assert.True(heading < actions, "the actions sit beside the history");
        Assert.True(app.ActionRows.Count > 0, "the chosen item's actions are listed");

        // The footer shows the keys: judged from the screenshot.
        AppHarness.SaveScreen(app.Picker, "the-parts-of-the-picker.png");
    });

    /// <summary>Scenario: one item is counted as such.</summary>
    [Fact(DisplayName = "one item is counted as such")]
    public Task OneItemIsCountedAsSuch() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("solo", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker).ToList();
        Assert.Contains("1 item", texts);
        Assert.DoesNotContain("1 items", texts);

        AppHarness.SaveScreen(app.Picker, "one-item-is-counted-as-such.png");
    });

    /// <summary>Scenario: a long history scrolls.</summary>
    [Fact(DisplayName = "a long history scrolls")]
    public Task ALongHistoryScrolls() => AppHarness.RunAsync(async app =>
    {
        for (var i = 1; i <= 8; i++)
        {
            await app.CopyAsync($"copy {i}", null);
        }

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);

        // Seven whole rows show.
        var fullyShown = 0;
        foreach (var row in app.HistoryRows)
        {
            if (AppHarness.IsFullyShown(row))
            {
                fullyShown++;
            }
        }
        Assert.Equal(7, fullyShown);

        // The count says "8 items".
        Assert.Contains("8 items", AppHarness.Texts(app.Picker).ToList());
        Assert.Equal(8, app.History.Items.Count);

        // The user moves the choice to the eighth.
        for (var i = 0; i < 7; i++)
        {
            await app.TapAsync(GestureKey.Down);
        }

        // The list scrolls so the chosen row shows.
        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);
        Assert.True(AppHarness.IsFullyShown(chosen), "the chosen row shows in full");

        // The eighth row is the oldest copy; it shows in its row and in the preview.
        var texts = AppHarness.Texts(app.Picker).ToList();
        var shown = texts.FindAll(t => t == "copy 1").Count;
        Assert.True(shown >= 2, "the eighth item is chosen and previewed");

        AppHarness.SaveScreen(app.Picker, "a-long-history-scrolls.png");
    }, new AppOptions { ScreenHeight = 800 });
}
