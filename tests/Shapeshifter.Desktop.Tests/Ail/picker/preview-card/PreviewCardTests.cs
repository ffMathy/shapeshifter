// ail-derived-from: PreviewCard@1662b9df49570a3b
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the PreviewCard scenarios: the larger view of the chosen item, above its actions.</summary>
public sealed class PreviewCardTests
{
    // A valid 1 × 1 PNG, written to disk for the scenario that copies the path of an image file.
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==";

    /// <summary>Scenario: code.</summary>
    [Fact(DisplayName = "code")]
    public Task Code() => AppHarness.RunAsync(async app =>
    {
        const string code = "dotnet run --project src/App";
        await app.CopyAsync(code, null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.Contains("28 characters", AppHarness.Texts(PickerOf(app)));

        var captions = TextBlocks(app, "28 characters");
        Assert.NotEmpty(captions);
        var proseFont = captions[0].FontFamily.Name;

        // The code is shown in a monospaced font: a different family from the strip's caption.
        Assert.Contains(TextBlocks(app, code), b => b.FontFamily.Name != proseFont);

        AppHarness.SaveScreen(app.Picker, "code.png");
    });

    /// <summary>Scenario: text of several lines.</summary>
    [Fact(DisplayName = "text of several lines")]
    public Task TextOfSeveralLines() => AppHarness.RunAsync(async app =>
    {
        const string text = "line one of three\nline two of three\nline three of three";
        await app.CopyAsync(text, null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.Contains(
            AppHarness.Find<TextBlock>(PickerOf(app)),
            b => b.Text is not null && b.Text.ReplaceLineEndings("\n") == text);
        Assert.Contains("3 lines · 55 characters", AppHarness.Texts(PickerOf(app)));

        AppHarness.SaveScreen(app.Picker, "text-of-several-lines.png");
    });

    /// <summary>Scenario: a colour.</summary>
    [Fact(DisplayName = "a colour")]
    public Task AColour() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("#2c5497", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var blue = Color.Parse("#2C5497");
        Assert.Contains(
            AppHarness.Find<Control>(PickerOf(app)),
            c => FillOf(c) == blue
                && Math.Abs(c.Bounds.Width - c.Bounds.Height) < 0.5
                && c.Bounds.Width > 40);

        var texts = AppHarness.Texts(PickerOf(app));
        Assert.Contains("#2C5497", texts);
        Assert.Contains("rgb(44, 84, 151)", texts);

        AppHarness.SaveScreen(app.Picker, "a-colour.png");
    });

    /// <summary>Scenario: an image.</summary>
    [Fact(DisplayName = "an image")]
    public Task AnImage() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(320, 200);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        const string caption = "Image · 320 × 200";
        Assert.Contains(caption, AppHarness.Texts(PickerOf(app)));
        AssertImageFillsCard(app, caption);

        AppHarness.SaveScreen(app.Picker, "an-image.png");
    });

    /// <summary>Scenario: files.</summary>
    [Fact(DisplayName = "files")]
    public Task Files() => AppHarness.RunAsync(async app =>
    {
        await app.CopyFilesAsync(["/tmp/files/notes.txt", "/tmp/files/report.pdf"]);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var together = AppHarness.Texts(PickerOf(app));
        Assert.Contains("notes.txt", together);
        Assert.Contains("report.pdf", together);
        Assert.Contains("2 files", together);

        await app.ReleaseCtrlAsync();

        await app.CopyFilesAsync(["/tmp/files/notes.txt"]);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var single = AppHarness.Texts(PickerOf(app));
        Assert.Contains("notes.txt", single);
        Assert.Contains("/tmp/files", single);

        AppHarness.SaveScreen(app.Picker, "files.png");
    });

    /// <summary>Scenario: the path of an image.</summary>
    [Fact(DisplayName = "the path of an image")]
    public Task ThePathOfAnImage() => AppHarness.RunAsync(async app =>
    {
        const string folder = "/tmp/pictures";
        const string path = "/tmp/pictures/sunset.png";
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(TinyPng));
        try
        {
            await app.CopyAsync(path, null);

            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            var texts = AppHarness.Texts(PickerOf(app));
            Assert.Contains(path, texts);
            Assert.Contains(folder, texts);
            AssertImageFillsCard(app, folder);

            AppHarness.SaveScreen(app.Picker, "the-path-of-an-image.png");
        }
        finally
        {
            File.Delete(path);
        }
    });

    /// <summary>Scenario: a search that finds nothing.</summary>
    [Fact(DisplayName = "a search that finds nothing")]
    public Task ASearchThatFindsNothing() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("hello", null);

        await app.HoldCtrlVAsync();
        await app.TypeSearchAsync("zzqq");

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(PickerOf(app));
        Assert.DoesNotContain("hello", texts);
        Assert.DoesNotContain("5 characters", texts);

        AppHarness.SaveScreen(app.Picker, "a-search-that-finds-nothing.png");
    });

    // The picker's window, which must be there once the picker has appeared.
    private static Visual PickerOf(AppHarness app)
    {
        Visual? picker = app.Picker;
        Assert.NotNull(picker);
        return picker;
    }

    private static List<TextBlock> TextBlocks(AppHarness app, string text)
    {
        var found = new List<TextBlock>();
        foreach (var block in AppHarness.Find<TextBlock>(PickerOf(app)))
        {
            if (block.Text == text)
            {
                found.Add(block);
            }
        }

        return found;
    }

    // An image that fills the card edge to edge spans at least the width of the strip's padded caption,
    // and is at least 168 high.
    private static void AssertImageFillsCard(AppHarness app, string caption)
    {
        var picker = PickerOf(app);
        var captions = TextBlocks(app, caption);
        Assert.NotEmpty(captions);
        var captionBounds = BoundsIn(captions[captions.Count - 1], picker);

        Assert.Contains(
            AppHarness.Find<Image>(picker),
            image =>
            {
                var bounds = BoundsIn(image, picker);
                return bounds.Height >= 168
                    && bounds.Left <= captionBounds.Left
                    && bounds.Right >= captionBounds.Right;
            });
    }

    private static Rect BoundsIn(Visual visual, Visual root)
    {
        var origin = visual.TranslatePoint(new Point(0, 0), root)
            ?? throw new InvalidOperationException("The control is not in the window.");
        return new Rect(origin, visual.Bounds.Size);
    }

    private static Color? FillOf(Control control)
    {
        var brush = control switch
        {
            Border border => border.Background,
            Panel panel => panel.Background,
            Shape shape => shape.Fill,
            Avalonia.Controls.Primitives.TemplatedControl templated => templated.Background,
            _ => null,
        };
        return brush is ISolidColorBrush solid ? solid.Color : null;
    }
}
