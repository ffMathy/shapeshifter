// ail-derived-from: BoxedSheet@a498c56d1888ec88
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Boxed Sheet look of the picker on Linux.</summary>
public sealed class BoxedSheetTests
{
    /// <summary>The picker on Linux shows the header, the boxed history and the boxed actions.</summary>
    [Fact(DisplayName = "the Linux picker")]
    public Task TheLinuxPicker() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Thursday", null);
        await app.CopyAsync("https://example.org/docs", null);
        await app.CopyAsync("SELECT * FROM users;", null);
        await app.CopyAsync("42 Elm Street", null);
        await app.CopyAsync("hello@example.org", null);
        await app.CopyAsync("git status", null);
        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker!);
        var joined = string.Join(" ", texts);

        // The header shows "Shapeshifter".
        Assert.Contains("Shapeshifter", texts);

        // "History" above the history's card, with "7 items" at the card's end.
        Assert.Equal(7, app.History.Items.Count);
        var history = joined.IndexOf("History", StringComparison.Ordinal);
        var count = joined.IndexOf("7 items", StringComparison.Ordinal);
        Assert.True(history >= 0, "\"History\" is shown");
        Assert.True(count >= 0, "\"7 items\" is shown");
        Assert.True(history < count, "\"History\" comes before \"7 items\"");

        // A row is chosen (its tint and ring are judged from the screenshot).
        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);

        // "Actions" and "→ to choose" head the actions' card, and "Paste" is among the actions.
        var actions = joined.IndexOf("Actions", StringComparison.Ordinal);
        var choose = joined.IndexOf("→ to choose", StringComparison.Ordinal);
        Assert.True(actions >= 0, "\"Actions\" is shown");
        Assert.True(choose >= 0, "\"→ to choose\" is shown");
        Assert.True(actions < choose, "\"Actions\" comes before \"→ to choose\"");
        Assert.Contains("Paste", texts);
        Assert.True(app.ActionRows.Count > 0);

        AppHarness.SaveScreen(app.Picker!, "the-linux-picker.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>Without a compositor the sheet fills its window, square, bordered and without shadow.</summary>
    [Fact(DisplayName = "without a compositor")]
    public Task WithoutACompositor() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.Contains("Shapeshifter", AppHarness.Texts(app.Picker!));

        AppHarness.SaveScreen(app.Picker!, "without-a-compositor.png");
    }, new AppOptions { Look = Look.Linux, NoCompositor = true });
}
