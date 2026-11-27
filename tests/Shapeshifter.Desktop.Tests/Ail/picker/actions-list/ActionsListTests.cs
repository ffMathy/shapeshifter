// ail-derived-from: ActionsList@9845a3dcfcd9456f
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the ActionsList view: the actions shown for the chosen item.</summary>
public sealed class ActionsListTests
{
    /// <summary>The actions of a text.</summary>
    [Fact(DisplayName = "the actions of a text")]
    public Task TheActionsOfAText() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.HoldCtrlVAsync();
        await AppHarness.WaitUntilAsync(() => app.ActionRows.Count >= 5, "the actions of the chosen text");

        var rows = app.ActionRows;
        Assert.Equal(5, rows.Count);

        string[] titles = ["Paste", "Paste as file", "Upload and paste link", "Search the web", "Pin to clipboard"];
        for (var i = 0; i < titles.Length; i++)
        {
            var texts = TextsOf(rows[i]);
            Assert.Contains(titles[i], texts);
            var title = titles[i];
            Assert.Contains(texts, t => t != title && t != "Default" && t.Trim().Length > 1);
        }

        Assert.Contains("Pastes clipboard contents as-is.", TextsOf(rows[0]));
        Assert.Contains("Saves this text to a .txt file and pastes the file.", TextsOf(rows[1]));

        AppHarness.SaveScreen(app.Picker, "the-actions-of-a-text.png");
    });

    /// <summary>Moving into the actions.</summary>
    [Fact(DisplayName = "moving into the actions")]
    public Task MovingIntoTheActions() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(640, 480);
        await app.HoldCtrlVAsync();
        await AppHarness.WaitUntilAsync(() => app.ActionRows.Count > 0, "the actions of the chosen image");

        await app.TapAsync(GestureKey.Right);

        var rows = app.ActionRows;
        Assert.True(rows.Count > 0);
        Assert.Contains("Paste", TextsOf(rows[0]));
        Assert.True(rows[0].IsSelected);
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.False(rows[i].IsSelected);
        }

        Assert.NotNull(app.ChosenRow);

        AppHarness.SaveScreen(app.Picker, "moving-into-the-actions.png");
    });

    /// <summary>The heading in the Windows look.</summary>
    [Fact(DisplayName = "the heading in the Windows look")]
    public Task TheHeadingInTheWindowsLook() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.HoldCtrlVAsync();
        await AppHarness.WaitUntilAsync(() => app.ActionRows.Count > 0, "the actions of the chosen text");

        Assert.Contains("Actions for this text", AppHarness.Texts(app.Picker));

        var rows = app.ActionRows;
        var pasteTexts = TextsOf(rows[0]);
        Assert.Contains("Paste", pasteTexts);
        Assert.Contains("Default", pasteTexts);
        for (var i = 1; i < rows.Count; i++)
        {
            Assert.DoesNotContain("Default", TextsOf(rows[i]));
        }

        AppHarness.SaveScreen(app.Picker, "the-heading-in-the-windows-look.png");
    }, new AppOptions { Look = Look.Windows });

    private static List<string> TextsOf(ListBoxItem row) =>
        row.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(t.Text))
            .Select(t => t.Text!)
            .ToList();
}
