// ail-derived-from: HistoryRow@0ed9841a6cc71312
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the HistoryRow view: what each row of the history shows.</summary>
public sealed class HistoryRowTests
{
    private static int IndexOf(IEnumerable<string> texts, string text)
    {
        var index = 0;
        foreach (var t in texts)
        {
            if (t == text)
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static bool IsShownInMedium(Window window, string text)
    {
        foreach (var block in AppHarness.Find<TextBlock>(window))
        {
            if (block.Text == text && block.FontWeight == FontWeight.Medium)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Taps the gesture key with the given name, through the harness's own TapAsync.</summary>
    private static async Task TapKeyAsync(object app, string key)
    {
        var method = app.GetType().GetMethods().Single(m => m.Name == "TapAsync");
        var parameters = method.GetParameters();
        var args = new object?[parameters.Length];
        args[0] = Enum.Parse(parameters[0].ParameterType, key);
        for (var i = 1; i < parameters.Length; i++)
        {
            args[i] = parameters[i].DefaultValue;
        }

        var task = (Task)method.Invoke(app, args)!;
        await task;
    }

    /// <summary>A text copied in another app shows its text, the window it came from and the time.</summary>
    [Fact(DisplayName = "a text copied in another app")]
    public Task ATextCopiedInAnotherApp() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("focus 7svfio", "Shapeshifter E2E Target");

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();
        var title = IndexOf(texts, "focus 7svfio");
        var source = IndexOf(texts, "Shapeshifter E2E Target");
        Assert.True(title >= 0, "the row shows the copied text");
        Assert.True(source >= 0, "the row shows the window it was copied in");
        Assert.True(title < source, "the window's name is below the text");
        Assert.Contains("Just now", texts);

        AppHarness.SaveScreen(picker, "a-text-copied-in-another-app.png");
    }, new AppOptions { Language = "en" });

    /// <summary>A text from an unknown window describes itself on its second line.</summary>
    [Fact(DisplayName = "a text from an unknown window describes itself")]
    public Task ATextFromAnUnknownWindowDescribesItself() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.CopyAsync("one\ntwo\nthree\nfour\n", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();
        Assert.Contains("28 characters", texts);
        Assert.Contains("5 lines", texts);

        AppHarness.SaveScreen(picker, "a-text-from-an-unknown-window-describes-itself.png");
    }, new AppOptions { Language = "en" });

    /// <summary>An image's row names it and its size.</summary>
    [Fact(DisplayName = "an image")]
    public Task AnImage() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(320, 200);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();
        Assert.Contains("Image · 320 × 200", texts);
        Assert.DoesNotContain(texts, t => t.EndsWith(" characters", StringComparison.Ordinal));
        Assert.DoesNotContain(texts, t => t.EndsWith(" lines", StringComparison.Ordinal));

        AppHarness.SaveScreen(picker, "an-image.png");
    }, new AppOptions { Language = "en" });

    /// <summary>Copied files show their names, and a single file its folder.</summary>
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
            await File.WriteAllTextAsync(notes, "notes");
            await File.WriteAllTextAsync(report, "report");

            await app.CopyFilesAsync([notes]);
            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            var single = AppHarness.Texts(app.Picker!).ToList();
            var name = IndexOf(single, "notes.txt");
            var folderName = IndexOf(single, "files");
            Assert.True(name >= 0, "the row shows the file's name");
            Assert.True(folderName >= 0, "the row shows the file's folder");
            Assert.True(name < folderName, "the folder is below the name");

            await TapKeyAsync(app, "Escape");
            await app.ReleaseCtrlAsync();

            await app.CopyFilesAsync([notes, report]);
            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            var picker = app.Picker!;
            var several = AppHarness.Texts(picker).ToList();
            Assert.Contains("2 items · notes.txt, report.pdf", several);

            AppHarness.SaveScreen(picker, "files.png");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }, new AppOptions { Language = "en" });

    /// <summary>A copied colour code fills its tile with that colour.</summary>
    [Fact(DisplayName = "a colour code")]
    public Task AColourCode() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("#2c5497", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        Assert.Contains("#2c5497", AppHarness.Texts(picker));
        var blue = Color.Parse("#2c5497");
        Assert.Contains(
            AppHarness.Find<Border>(picker),
            border => border.Background is ISolidColorBrush brush && brush.Color == blue);

        AppHarness.SaveScreen(picker, "a-colour-code.png");
    }, new AppOptions { Language = "en" });

    /// <summary>A pinned item keeps the time it was pinned, shown after its pin.</summary>
    [Fact(DisplayName = "a pinned item")]
    public Task APinnedItem() => AppHarness.RunAsync(async app =>
    {
        await app.PinAsync("public void Main() {", TimeSpan.FromMinutes(5));

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();
        Assert.Contains("public void Main() {", texts);
        Assert.Contains("5 min ago", texts);
        Assert.True(app.History.Items[0].IsPinned, "the pinned item sits at the top");

        AppHarness.SaveScreen(picker, "a-pinned-item.png");
    }, new AppOptions { Language = "en" });

    /// <summary>The chosen row stands out, its title in medium weight.</summary>
    [Fact(DisplayName = "the chosen row")]
    public Task TheChosenRow() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("gamma first", null);
        await app.CopyAsync("beta second", null);
        await app.CopyAsync("alpha third", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        for (var step = 0; step < 3 && !IsShownInMedium(picker, "beta second"); step++)
        {
            await TapKeyAsync(app, "Down");
        }

        Assert.True(IsShownInMedium(picker, "beta second"), "the chosen row's title is medium weight");
        Assert.False(IsShownInMedium(picker, "alpha third"), "a row not chosen is regular weight");
        Assert.False(IsShownInMedium(picker, "gamma first"), "a row not chosen is regular weight");

        AppHarness.SaveScreen(picker, "the-chosen-row.png");
    }, new AppOptions { Language = "en" });
}
