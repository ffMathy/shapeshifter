// ail-derived-from: PasteWithoutLocation@be1019cbdab18617
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using SkiaSharp;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteWithoutLocation node.</summary>
public sealed class PasteWithoutLocationTests
{
    private static string FixturePath =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "photo-with-location.jpg");

    private static byte[] FixtureBytes() => File.ReadAllBytes(FixturePath);

    private static byte[] SkiaPng()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Red);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static (SKEncodedImageFormat Format, int Width, int Height) Describe(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        return (codec.EncodedFormat, codec.Info.Width, codec.Info.Height);
    }

    private static string WriteSourceFile(ActionTestHarness harness, string name, byte[] bytes)
    {
        var directory = Path.Combine(harness.Context.Paths.TempDirectory, "source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>example 1</summary>
    [Fact(DisplayName = "example 1: HasLocation(the fixture's bytes) => true")]
    public void Example01HasLocationOfFixtureIsTrue()
    {
        Assert.True(PasteWithoutLocationAction.HasLocation(FixtureBytes()));
    }

    /// <summary>example 2</summary>
    [Fact(DisplayName = "example 2: HasLocation(a PNG made by SkiaSharp) => false")]
    public void Example02HasLocationOfSkiaPngIsFalse()
    {
        Assert.False(PasteWithoutLocationAction.HasLocation(SkiaPng()));
    }

    /// <summary>example 3</summary>
    [Fact(DisplayName = "example 3: HasLocation([1, 2, 3]) => false")]
    public void Example03HasLocationOfGarbageIsFalse()
    {
        Assert.False(PasteWithoutLocationAction.HasLocation([1, 2, 3]));
    }

    /// <summary>example 4</summary>
    [Fact(DisplayName = "example 4: Clean(the fixture's bytes) => a JPEG of 40 × 30 for which HasLocation is false")]
    public void Example04CleanFixtureGivesJpegWithoutLocation()
    {
        var cleaned = PasteWithoutLocationAction.Clean(FixtureBytes());

        var (format, width, height) = Describe(cleaned);
        Assert.Equal(SKEncodedImageFormat.Jpeg, format);
        Assert.Equal(40, width);
        Assert.Equal(30, height);
        Assert.False(PasteWithoutLocationAction.HasLocation(cleaned));
    }

    /// <summary>example 5</summary>
    [Fact(DisplayName = "example 5: CanExecute(an Image item of the fixture's bytes) => true")]
    public void Example05CanExecuteImageItemOfFixture()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = FixtureBytes() });

        Assert.True(new PasteWithoutLocationAction().CanExecute(item));
    }

    /// <summary>example 6</summary>
    [Fact(DisplayName = "example 6: CanExecute(files [a copy of the fixture]) => true")]
    public void Example06CanExecuteFileCopyOfFixture()
    {
        using var harness = new ActionTestHarness();
        var path = WriteSourceFile(harness, "photo.jpg", FixtureBytes());
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.True(new PasteWithoutLocationAction().CanExecute(item));
    }

    /// <summary>example 7</summary>
    [Fact(DisplayName = "example 7: CanExecute(files [a PNG file without metadata]) => false")]
    public void Example07CanExecutePngFileWithoutMetadata()
    {
        using var harness = new ActionTestHarness();
        var path = WriteSourceFile(harness, "plain.png", SkiaPng());
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.False(new PasteWithoutLocationAction().CanExecute(item));
    }

    /// <summary>example 8</summary>
    [Fact(DisplayName = "example 8: CanExecute(text \"hello\") => false")]
    public void Example08CanExecuteTextIsFalse()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        Assert.False(new PasteWithoutLocationAction().CanExecute(item));
    }

    /// <summary>example 9</summary>
    [Fact(DisplayName = "example 9: ExecuteAsync(an Image item of the fixture's bytes) => pastes { Image: a JPEG without a position }; the history is unchanged")]
    public async Task Example09ExecuteImageItemPastesCleanJpeg()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = FixtureBytes() });
        var historyCount = harness.History.Items.Count;

        await new PasteWithoutLocationAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.NotNull(written.Image);
        var image = written.Image;
        Assert.Equal(SKEncodedImageFormat.Jpeg, Describe(image).Format);
        Assert.False(PasteWithoutLocationAction.HasLocation(image));
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }

    /// <summary>example 10</summary>
    [Fact(DisplayName = "example 10: ExecuteAsync(files [\"…/holiday.jpg\", a copy of the fixture]) => pastes { Files: [\"<temp>/Without location/<8 hex>/holiday.jpg\"] }, a JPEG without a position; the original file is unchanged")]
    public async Task Example10ExecuteFileItemPastesCleanCopy()
    {
        using var harness = new ActionTestHarness();
        var original = FixtureBytes();
        var path = WriteSourceFile(harness, "holiday.jpg", original);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        await new PasteWithoutLocationAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        var pasted = Assert.Single(written.Files);
        Assert.Equal("holiday.jpg", Path.GetFileName(pasted));

        var randomDirectory = Directory.GetParent(Path.GetFullPath(pasted));
        Assert.NotNull(randomDirectory);
        Assert.Equal(8, randomDirectory.Name.Length);
        Assert.True(randomDirectory.Name.All(char.IsAsciiHexDigit), randomDirectory.Name);

        var withoutLocation = randomDirectory.Parent;
        Assert.NotNull(withoutLocation);
        Assert.Equal(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(harness.Context.Paths.TempDirectory, "Without location"))),
            Path.TrimEndingDirectorySeparator(withoutLocation.FullName));

        var pastedBytes = File.ReadAllBytes(pasted);
        Assert.Equal(SKEncodedImageFormat.Jpeg, Describe(pastedBytes).Format);
        Assert.False(PasteWithoutLocationAction.HasLocation(pastedBytes));
        Assert.Equal(1, harness.Paste.Calls);

        Assert.Equal(original, File.ReadAllBytes(path));
    }
}
