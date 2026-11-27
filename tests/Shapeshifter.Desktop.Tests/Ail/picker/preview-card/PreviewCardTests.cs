// ail-derived-from: PreviewCard@1662b9df49570a3b
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the preview card: the larger view of the chosen item, above its actions.</summary>
public sealed class PreviewCardTests
{
    private const string TinyPng =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private static bool IsMonospaced(TextBlock block)
    {
        var name = block.FontFamily.Name;
        return name.Contains("mono", StringComparison.OrdinalIgnoreCase)
            || name.Contains("consolas", StringComparison.OrdinalIgnoreCase)
            || name.Contains("menlo", StringComparison.OrdinalIgnoreCase)
            || name.Contains("courier", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsColour(IBrush? brush, Color colour) =>
        brush is ISolidColorBrush solid && solid.Color == colour;

    private static bool HasSwatch(Window window, Color colour)
    {
        foreach (var border in AppHarness.Find<Border>(window))
        {
            if (IsColour(border.Background, colour))
            {
                return true;
            }
        }

        foreach (var rectangle in AppHarness.Find<Rectangle>(window))
        {
            if (IsColour(rectangle.Fill, colour))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShowsPreviewImage(Window window)
    {
        foreach (var image in AppHarness.Find<Image>(window))
        {
            if (image.Source is not null && image.IsVisible && image.Bounds.Width > 100)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Code is shown in a monospaced font with its character count in the strip.</summary>
    [Fact(DisplayName = "code")]
    public Task Code() => AppHarness.RunAsync(async app =>
    {
        const string text = "dotnet run --project src/App";
        await app.CopyAsync(text, null);

        await app.HoldCtrlVAsync();

        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("28 characters"),
            "the strip to say 28 characters");
        Assert.Contains(
            AppHarness.Find<TextBlock>(app.Picker),
            block => block.Text == text && IsMonospaced(block));
        Assert.Contains("28 characters", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "code.png");
    });

    /// <summary>A text of several lines keeps its line breaks and the strip counts lines and characters.</summary>
    [Fact(DisplayName = "text of several lines")]
    public Task TextOfSeveralLines() => AppHarness.RunAsync(async app =>
    {
        const string text = "line one of three\nline two of three\nline three of three";
        await app.CopyAsync(text, null);

        await app.HoldCtrlVAsync();

        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("3 lines · 55 characters"),
            "the strip to say 3 lines · 55 characters");
        Assert.Contains(text, AppHarness.Texts(app.Picker));
        Assert.Contains("3 lines · 55 characters", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "text-of-several-lines.png");
    });

    /// <summary>A colour code shows a swatch, the code in capitals, and its rgb value in the strip.</summary>
    [Fact(DisplayName = "a colour")]
    public Task AColour() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("#2c5497", null);

        await app.HoldCtrlVAsync();
        Assert.True(app.HistoryRows[0].IsSelected);

        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("rgb(44, 84, 151)"),
            "the strip to say rgb(44, 84, 151)");
        var blue = Color.FromRgb(0x2C, 0x54, 0x97);
        Assert.True(HasSwatch(app.Picker, blue), "the card shows a #2C5497 swatch");
        Assert.Contains("#2C5497", AppHarness.Texts(app.Picker));
        Assert.Contains("rgb(44, 84, 151)", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "a-colour.png");
    });

    /// <summary>An image fills the card and the strip names its size.</summary>
    [Fact(DisplayName = "an image")]
    public Task AnImage() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(320, 200);

        await app.HoldCtrlVAsync();
        Assert.True(app.HistoryRows[0].IsSelected);

        await AppHarness.WaitUntilAsync(() => ShowsPreviewImage(app.Picker), "the preview image");
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("Image · 320 × 200"),
            "the strip to say Image · 320 × 200");
        Assert.Contains("Image · 320 × 200", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "an-image.png");
    });

    /// <summary>Several files are listed with a count; a single file shows its name and its folder.</summary>
    [Fact(DisplayName = "files")]
    public async Task Files()
    {
        await AppHarness.RunAsync(async app =>
        {
            await app.CopyFilesAsync(["/tmp/files/notes.txt", "/tmp/files/report.pdf"]);

            await app.HoldCtrlVAsync();

            await AppHarness.WaitUntilAsync(
                () => AppHarness.Texts(app.Picker).Contains("2 files"),
                "the strip to say 2 files");
            var texts = AppHarness.Texts(app.Picker);
            Assert.Contains("notes.txt", texts);
            Assert.Contains("report.pdf", texts);
            Assert.Contains("2 files", texts);
        });

        await AppHarness.RunAsync(async app =>
        {
            await app.CopyFilesAsync(["/tmp/files/notes.txt"]);

            await app.HoldCtrlVAsync();

            await AppHarness.WaitUntilAsync(
                () => AppHarness.Texts(app.Picker).Contains("/tmp/files"),
                "the strip to show /tmp/files");
            var texts = AppHarness.Texts(app.Picker);
            Assert.Contains("notes.txt", texts);
            Assert.Contains("/tmp/files", texts);

            AppHarness.SaveScreen(app.Picker, "files.png");
        });
    }

    /// <summary>The path of an existing image file shows that image with the path over it and its folder in the strip.</summary>
    [Fact(DisplayName = "the path of an image")]
    public Task ThePathOfAnImage() => AppHarness.RunAsync(async app =>
    {
        const string path = "/tmp/pictures/sunset.png";
        Directory.CreateDirectory("/tmp/pictures");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String(TinyPng));
        try
        {
            await app.CopyAsync(path, null);

            await app.HoldCtrlVAsync();
            Assert.True(app.HistoryRows[0].IsSelected);

            await AppHarness.WaitUntilAsync(() => ShowsPreviewImage(app.Picker), "the image of the path");
            await AppHarness.WaitUntilAsync(
                () => AppHarness.Texts(app.Picker).Contains("/tmp/pictures"),
                "the strip to show /tmp/pictures");
            var texts = AppHarness.Texts(app.Picker);
            Assert.Contains(path, texts);
            Assert.Contains("/tmp/pictures", texts);

            AppHarness.SaveScreen(app.Picker, "the-path-of-an-image.png");
        }
        finally
        {
            File.Delete(path);
        }
    });

    /// <summary>A search without results leaves the card in place, empty.</summary>
    [Fact(DisplayName = "a search that finds nothing")]
    public Task ASearchThatFindsNothing() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("hello world", null);

        await app.HoldCtrlVAsync();
        await app.TypeSearchAsync("zzqq");

        await AppHarness.WaitUntilAsync(() => app.HistoryRows.Count == 0, "no results");
        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker);
        Assert.DoesNotContain("hello world", texts);
        Assert.DoesNotContain("11 characters", texts);

        AppHarness.SaveScreen(app.Picker, "a-search-that-finds-nothing.png");
    });
}
