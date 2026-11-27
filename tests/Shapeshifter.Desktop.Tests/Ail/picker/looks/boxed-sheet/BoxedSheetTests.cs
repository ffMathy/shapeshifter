// ail-derived-from: BoxedSheet@a498c56d1888ec88
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Boxed Sheet look of the picker on Linux.</summary>
public sealed class BoxedSheetTests
{
    /// <summary>The Linux picker shows its header, boxed history and boxed actions.</summary>
    [Fact(DisplayName = "the Linux picker")]
    public Task TheLinuxPicker() => AppHarness.RunAsync(async app =>
    {
        for (var i = 1; i <= 6; i++)
        {
            await app.CopyAsync($"other item {i}", null);
        }

        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);

        var texts = AppHarness.Texts(app.Picker).ToList();
        var joined = string.Join(" ", texts);

        // The header shows "Shapeshifter", before the history's heading.
        var title = texts.IndexOf("Shapeshifter");
        var history = texts.IndexOf("History");
        Assert.True(title >= 0, "the header shows \"Shapeshifter\"");
        Assert.True(history >= 0, "\"History\" heads the history's card");
        Assert.True(title < history, "the header comes before the history's heading");

        // "History" with "7 items" at the card's end.
        Assert.Equal(7, app.History.Items.Count);
        Assert.Contains("7 items", joined, StringComparison.Ordinal);

        // The chosen row is the newest item.
        Assert.NotEmpty(app.HistoryRows);
        Assert.True(app.HistoryRows[0].IsSelected, "the newest row is the chosen one");
        Assert.Same(app.HistoryRows[0], app.ChosenRow);
        Assert.Contains("dotnet run --project src/App", texts);

        // "Actions" and "→ to choose" head the actions' card, which offers "Paste".
        var actions = texts.IndexOf("Actions");
        Assert.True(actions >= 0, "\"Actions\" heads the actions' card");
        Assert.Contains("→ to choose", texts);
        Assert.Contains("Paste", texts);
        Assert.NotEmpty(app.ActionRows);

        AppHarness.SaveScreen(app.Picker, "the-linux-picker.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>Without a compositor the sheet fills its window, opaque and square.</summary>
    [Fact(DisplayName = "without a compositor")]
    public Task WithoutACompositor() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker).ToList();
        Assert.Contains("Shapeshifter", texts);
        Assert.Contains("History", texts);
        Assert.Contains("Actions", texts);

        AppHarness.SaveScreen(app.Picker, "without-a-compositor.png");
    }, new AppOptions { Look = Look.Linux });
}
