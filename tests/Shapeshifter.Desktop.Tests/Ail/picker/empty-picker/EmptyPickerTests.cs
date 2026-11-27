// ail-derived-from: EmptyPicker@4acad5c12853298b
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the EmptyPicker scenarios: the picker explains Shapeshifter when nothing was copied.</summary>
public sealed class EmptyPickerTests
{
    private const string Title = "Nothing copied yet";

    private const string FirstParagraph = "Copy as usual, and Shapeshifter keeps everything you copy.";

    private const string SecondParagraph =
        "To paste something you copied earlier, hold Ctrl+V until the history appears. Keep holding Ctrl, press V or the arrow keys to choose, and release Ctrl to paste. Press Space to keep the history open without holding the key, then Enter to paste.";

    /// <summary>Scenario: nothing copied yet.</summary>
    [Fact(DisplayName = "nothing copied yet")]
    public Task NothingCopiedYet() => AppHarness.RunAsync(async app =>
    {
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        List<string> texts = [.. AppHarness.Texts(app.Picker!)];
        var joined = string.Join("\n", texts);

        var titleAt = joined.IndexOf(Title, StringComparison.Ordinal);
        var firstAt = joined.IndexOf(FirstParagraph, StringComparison.Ordinal);
        var secondAt = joined.IndexOf(SecondParagraph, StringComparison.Ordinal);
        Assert.True(titleAt >= 0, $"The picker does not show \"{Title}\". It shows: {joined}");
        Assert.True(firstAt > titleAt, $"\"{FirstParagraph}\" is not shown below the title. It shows: {joined}");
        Assert.True(secondAt > firstAt, $"\"{SecondParagraph}\" is not shown after the first paragraph. It shows: {joined}");

        // The footer is what the picker shows after the explanation.
        var explanationEnd = -1;
        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i].Contains("then Enter to paste.", StringComparison.Ordinal))
            {
                explanationEnd = i;
            }
        }

        Assert.True(explanationEnd >= 0);
        var emptyFooter = texts.GetRange(explanationEnd + 1, texts.Count - explanationEnd - 1);
        Assert.NotEmpty(emptyFooter);

        AppHarness.SaveScreen(app.Picker!, "nothing-copied-yet.png");

        // For comparison: the footer of a picker with items.
        await app.ReleaseCtrlAsync();
        await app.CopyAsync("hello", null);
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        List<string> withItems = [.. AppHarness.Texts(app.Picker!)];
        Assert.True(withItems.Count >= emptyFooter.Count);
        var itemsFooter = withItems.GetRange(withItems.Count - emptyFooter.Count, emptyFooter.Count);
        Assert.Equal(itemsFooter, emptyFooter);
    });

    /// <summary>Scenario: in German.</summary>
    [Fact(DisplayName = "in German")]
    public Task InGerman() => AppHarness.RunAsync(async app =>
    {
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        List<string> texts = [.. AppHarness.Texts(app.Picker!)];
        var joined = string.Join("\n", texts);

        var titleAt = texts.IndexOf("Noch nichts kopiert");
        Assert.True(titleAt >= 0, $"The picker does not show \"Noch nichts kopiert\". It shows: {joined}");

        var namesStrg = false;
        for (var i = titleAt + 1; i < texts.Count; i++)
        {
            if (texts[i].Contains("Strg+V", StringComparison.Ordinal))
            {
                namesStrg = true;
            }
        }

        Assert.True(namesStrg, $"The explanation does not name \"Strg\". It shows: {joined}");

        AppHarness.SaveScreen(app.Picker!, "in-german.png");
    }, new AppOptions { Language = "de" });
}
