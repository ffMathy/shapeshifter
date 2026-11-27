// ail-derived-from: HistoryRow@9b01e1d5e807caa8
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the HistoryRow view: what each row of the history shows.</summary>
public sealed class HistoryRowTests
{
    /// <summary>A text copied in a named window shows its content, that window and the time.</summary>
    [Fact(DisplayName = "a text copied in another app")]
    public Task ATextCopiedInAnotherApp() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("focus 7svfio", "Shapeshifter E2E Target");

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.NotEmpty(app.HistoryRows);
        var texts = RowTexts(app.HistoryRows[0]);
        var content = texts.IndexOf("focus 7svfio");
        var window = texts.IndexOf("Shapeshifter E2E Target");
        var time = texts.IndexOf("Just now");
        Assert.True(content >= 0, $"the row shows the text; it shows: {string.Join(" | ", texts)}");
        Assert.True(window > content, $"the window's name follows the text; the row shows: {string.Join(" | ", texts)}");
        Assert.True(time > window, $"\"Just now\" is at the end; the row shows: {string.Join(" | ", texts)}");

        AppHarness.SaveScreen(app.Picker, "a-text-copied-in-another-app.png");
    });

    /// <summary>A text from no named window describes itself on its second line.</summary>
    [Fact(DisplayName = "a text from an unknown window describes itself")]
    public Task ATextFromAnUnknownWindowDescribesItself() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.CopyAsync("one\ntwo\nthree\nfour\n", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.True(app.HistoryRows.Count >= 2, "both texts have rows");
        var fourLines = RowTexts(app.HistoryRows[0]);
        var dotnet = RowTexts(app.HistoryRows[1]);
        Assert.Contains("28 characters", dotnet);
        Assert.Contains("5 lines", fourLines);

        AppHarness.SaveScreen(app.Picker, "a-text-from-an-unknown-window-describes-itself.png");
    });

    /// <summary>A copied image shows itself and its size.</summary>
    [Fact(DisplayName = "an image")]
    public Task AnImage() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(320, 200);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.NotEmpty(app.HistoryRows);
        var row = app.HistoryRows[0];
        await AppHarness.WaitUntilAsync(() => HasPicture(row), "the image's thumbnail");
        var texts = RowTexts(row);
        Assert.Contains("Image · 320 × 200", texts);
        var others = texts.Where(t => t != "Image · 320 × 200" && t != "Just now").ToList();
        Assert.True(others.Count == 0, $"no second line; the row also shows: {string.Join(" | ", others)}");

        AppHarness.SaveScreen(app.Picker, "an-image.png");
    });

    /// <summary>Copied files show their names and, for a single file, its folder.</summary>
    [Fact(DisplayName = "files")]
    public Task Files() => AppHarness.RunAsync(async app =>
    {
        var root = Path.Combine(Path.GetTempPath(), "history-row-" + Guid.NewGuid().ToString("N"));
        var folder = Path.Combine(root, "files");
        Directory.CreateDirectory(folder);
        try
        {
            var notes = Path.Combine(folder, "notes.txt");
            var report = Path.Combine(folder, "report.pdf");
            await File.WriteAllTextAsync(notes, "notes", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(report, "report", TestContext.Current.CancellationToken);

            await app.CopyFilesAsync([notes]);
            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            Assert.NotEmpty(app.HistoryRows);
            var single = RowTexts(app.HistoryRows[0]);
            var name = single.IndexOf("notes.txt");
            var place = single.IndexOf("files");
            Assert.True(name >= 0, $"the row names the file; it shows: {string.Join(" | ", single)}");
            Assert.True(place > name, $"the folder follows the name; the row shows: {string.Join(" | ", single)}");

            await app.ReleaseCtrlAsync();

            await app.CopyFilesAsync([notes, report]);
            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            Assert.NotEmpty(app.HistoryRows);
            Assert.Contains("2 items · notes.txt, report.pdf", RowTexts(app.HistoryRows[0]));

            AppHarness.SaveScreen(app.Picker, "files.png");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    });

    /// <summary>A copied colour code shows its code.</summary>
    [Fact(DisplayName = "a colour code")]
    public Task AColourCode() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("#2c5497", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.NotEmpty(app.HistoryRows);
        Assert.Contains("#2c5497", RowTexts(app.HistoryRows[0]));

        AppHarness.SaveScreen(app.Picker, "a-colour-code.png");
    });

    /// <summary>A pinned item sits at the top and keeps its time.</summary>
    [Fact(DisplayName = "a pinned item")]
    public Task APinnedItem() => AppHarness.RunAsync(async app =>
    {
        await app.PinAsync("public void Main() {", TimeSpan.FromMinutes(5));

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.NotEmpty(app.HistoryRows);
        var texts = RowTexts(app.HistoryRows[0]);
        var content = texts.IndexOf("public void Main() {");
        var time = texts.IndexOf("5 min ago");
        Assert.True(content >= 0, $"the top row is the pinned item; it shows: {string.Join(" | ", texts)}");
        Assert.True(time > content, $"\"5 min ago\" is at the end; the row shows: {string.Join(" | ", texts)}");

        AppHarness.SaveScreen(app.Picker, "a-pinned-item.png");
    });

    /// <summary>The chosen row is marked as chosen.</summary>
    [Fact(DisplayName = "the chosen row")]
    public Task TheChosenRow() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copy", null);
        await app.CopyAsync("second copy", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.True(app.HistoryRows.Count >= 2, "both texts have rows");
        for (var tries = 0; tries < 10 && !app.HistoryRows[1].IsSelected; tries++)
        {
            var chosen = IndexOfChosen(app.HistoryRows);
            await app.TapAsync(chosen < 1 ? GestureKey.Down : GestureKey.Up);
        }

        Assert.True(app.HistoryRows[1].IsSelected, "the second row is chosen");
        Assert.False(app.HistoryRows[0].IsSelected, "the first row is not chosen");
        Assert.Same(app.HistoryRows[1], app.ChosenRow);

        AppHarness.SaveScreen(app.Picker, "the-chosen-row.png");
    });

    private static List<string> RowTexts(Control row) =>
        row.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text))
            .Select(t => t.Text!)
            .ToList();

    private static bool HasPicture(Control row) =>
        row.GetVisualDescendants().Any(v =>
            v is Image { IsEffectivelyVisible: true, Source: not null }
            || v is Border { IsEffectivelyVisible: true, Background: ImageBrush { Source: not null } });

    private static int IndexOfChosen(IReadOnlyList<ListBoxItem> rows)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].IsSelected)
            {
                return i;
            }
        }

        return -1;
    }
}
