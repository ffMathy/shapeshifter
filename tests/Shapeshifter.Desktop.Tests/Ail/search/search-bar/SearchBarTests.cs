// ail-derived-from: SearchBar@a9735bfd1c003f47
using Avalonia.Controls;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the SearchBar view: the search box, its results and its empty state in the picker.</summary>
public sealed class SearchBarTests
{
    private static readonly string[] SearchFooter =
    [
        "Enter", "Paste",
        "↑↓", "Move",
        "←→", "Actions",
        "Esc", "Cancel",
    ];

    /// <summary>Taps the harness's Search key (S) through the app's own TapAsync, whatever namespace its key type lives in.</summary>
    private static Task TapSearchAsync(object app)
    {
        var tap = app.GetType().GetMethods().Single(m =>
            m.Name == "TapAsync"
            && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType.IsEnum);
        var key = Enum.Parse(tap.GetParameters()[0].ParameterType, "Search");
        return (Task?)tap.Invoke(app, [key])
            ?? throw new InvalidOperationException("TapAsync returned no task.");
    }

    /// <summary>Scenario: the search begins.</summary>
    [Fact(DisplayName = "the search begins")]
    public Task TheSearchBegins() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("apple pie recipe", null);
        await app.CopyAsync("banana bread", null);

        await app.HoldCtrlVAsync();
        await TapSearchAsync(app);

        var picker = app.Picker;
        Assert.NotNull(picker);
        Assert.True(app.PickerVisible);

        var boxes = AppHarness.Find<TextBox>(picker).ToList();
        Assert.Single(boxes);

        var texts = AppHarness.Texts(picker).ToList();
        var title = texts.IndexOf("Shapeshifter");
        var placeholder = texts.IndexOf("Search everything you copied");
        Assert.True(title >= 0, "The picker shows its title.");
        Assert.True(placeholder >= 0, "The search box shows its placeholder.");
        Assert.True(title < placeholder, "The search box sits under the title.");

        Assert.True(texts.Count >= SearchFooter.Length);
        var footer = texts.GetRange(texts.Count - SearchFooter.Length, SearchFooter.Length);
        Assert.Equal<string>(SearchFooter, footer);

        AppHarness.SaveScreen(picker, "the-search-begins.png");
    });

    /// <summary>Scenario: the results.</summary>
    [Fact(DisplayName = "the results")]
    public Task TheResults() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("apple pie recipe", null);
        await app.CopyAsync("banana bread", null);
        await app.CopyAsync("cherry tart", null);

        await app.HoldCtrlVAsync();
        await TapSearchAsync(app);
        await app.TypeSearchAsync("banana");

        var picker = app.Picker;
        Assert.NotNull(picker);
        Assert.True(app.PickerVisible);

        var texts = AppHarness.Texts(picker).ToList();
        Assert.Contains("1 item", texts);
        Assert.Contains("History", texts);
        Assert.DoesNotContain("apple pie recipe", texts);
        Assert.DoesNotContain("cherry tart", texts);

        // Listed once in the history pane and once more in the preview of the chosen result.
        Assert.True(
            texts.FindAll(t => t == "banana bread").Count >= 2,
            "The only result is listed and shown in the preview as the chosen item.");

        // The actions pane heading beside the preview, besides the footer's "Actions".
        Assert.True(texts.FindAll(t => t == "Actions").Count >= 2, "The actions pane is shown beside the results.");

        AppHarness.SaveScreen(picker, "the-results.png");
    });

    /// <summary>Scenario: nothing matches.</summary>
    [Fact(DisplayName = "nothing matches")]
    public Task NothingMatches() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("apple pie recipe", null);

        await app.HoldCtrlVAsync();
        await TapSearchAsync(app);

        var picker = app.Picker;
        Assert.NotNull(picker);
        var widthWithList = picker.Bounds.Width;

        await app.TypeSearchAsync("zzz");

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(picker).ToList();

        Assert.Contains("No matches", texts);
        Assert.Contains("Nothing you copied contains “zzz”.", texts);
        Assert.DoesNotContain("History", texts);
        Assert.DoesNotContain("1 item", texts);
        Assert.DoesNotContain(texts, t => t.EndsWith(" items", StringComparison.Ordinal));
        Assert.DoesNotContain("apple pie recipe", texts);

        // The actions pane keeps its place: its heading besides the footer's "Actions".
        Assert.True(texts.FindAll(t => t == "Actions").Count >= 2, "The actions pane keeps its place.");
        Assert.Equal(widthWithList, picker.Bounds.Width);

        AppHarness.SaveScreen(picker, "nothing-matches.png");
    });
}
