// ail-derived-from: PasteSmallerImage@198e82c7fd3bcd39
using System.Globalization;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using SkiaSharp;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteSmallerImage node.</summary>
public sealed class PasteSmallerImageTests
{
    private static byte[] Encode(int width, int height, SKEncodedImageFormat format, int quality)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(new SKColor(0x35, 0x84, 0xE4));
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, quality);
        return data.ToArray();
    }

    private static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png, 100);

    private static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg, 90);

    private static (SKEncodedImageFormat Format, int Width, int Height) Inspect(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        Assert.NotNull(codec);
        return (codec.EncodedFormat, codec.Info.Width, codec.Info.Height);
    }

    [Fact(DisplayName = "example 1: NewSize(3840, 2160) => { 1920, 1080 }")]
    public void Example1()
    {
        var size = PasteSmallerImageAction.NewSize(3840, 2160);
        Assert.Equal(1920, size.Width);
        Assert.Equal(1080, size.Height);
    }

    [Fact(DisplayName = "example 2: NewSize(1000, 4000) => { 480, 1920 }")]
    public void Example2()
    {
        var size = PasteSmallerImageAction.NewSize(1000, 4000);
        Assert.Equal(480, size.Width);
        Assert.Equal(1920, size.Height);
    }

    [Fact(DisplayName = "example 3: NewSize(2000, 3) => { 1920, 3 }")]
    public void Example3()
    {
        var size = PasteSmallerImageAction.NewSize(2000, 3);
        Assert.Equal(1920, size.Width);
        Assert.Equal(3, size.Height);
    }

    [Fact(DisplayName = "example 4: NewSize(5000, 1) => { 1920, 1 }; never below 1")]
    public void Example4()
    {
        var size = PasteSmallerImageAction.NewSize(5000, 1);
        Assert.Equal(1920, size.Width);
        Assert.Equal(1, size.Height);
    }

    [Fact(DisplayName = "example 5: Measure(a PNG of 2560 × 1440) => { 2560, 1440 }")]
    public void Example5()
    {
        var size = PasteSmallerImageAction.Measure(Png(2560, 1440));
        Assert.Equal<int?>(2560, size?.Width);
        Assert.Equal<int?>(1440, size?.Height);
    }

    [Fact(DisplayName = "example 6: Measure([1, 2, 3]) => nothing")]
    public void Example6()
    {
        var size = PasteSmallerImageAction.Measure([1, 2, 3]);
        Assert.Null(size);
    }

    [Fact(DisplayName = "example 7: CanExecute(a PNG image of 2560 × 1440) => true;  GetDescription => \"Pastes this image scaled down to 1920 × 1080 pixels.\"")]
    public void Example7()
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("en");
            CultureInfo.CurrentCulture = new CultureInfo("en");
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png(2560, 1440) });
            var action = new PasteSmallerImageAction();
            Assert.True(action.CanExecute(item));
            Assert.Equal("Pastes this image scaled down to 1920 × 1080 pixels.", action.GetDescription(item));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "example 8: CanExecute(a PNG image of 1920 × 1200) => false; not larger than MaxSide")]
    public void Example8()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png(1920, 1200) });
        Assert.False(new PasteSmallerImageAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 9: CanExecute(image bytes SkiaSharp cannot read) => false")]
    public void Example9()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = [1, 2, 3] });
        Assert.False(new PasteSmallerImageAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 10: CanExecute(text \"hello\") => false")]
    public void Example10()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });
        Assert.False(new PasteSmallerImageAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 11: Shrink(a PNG of 2560 × 1440) => a PNG of 1920 × 1080")]
    public void Example11()
    {
        var result = Inspect(PasteSmallerImageAction.Shrink(Png(2560, 1440)));
        Assert.Equal(SKEncodedImageFormat.Png, result.Format);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
    }

    [Fact(DisplayName = "example 12: Shrink(a JPEG of 4000 × 3000) => a JPEG of 1920 × 1440")]
    public void Example12()
    {
        var result = Inspect(PasteSmallerImageAction.Shrink(Jpeg(4000, 3000)));
        Assert.Equal(SKEncodedImageFormat.Jpeg, result.Format);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1440, result.Height);
    }

    [Fact(DisplayName = "example 13: ExecuteAsync(a PNG image of 2560 × 1440) => pastes { Image: a PNG of 1920 × 1080 }; the history is unchanged")]
    public async Task Example13()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png(2560, 1440) });
        var historyCount = harness.History.Items.Count;

        await new PasteSmallerImageAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Backend.Writes);
        var written = harness.Backend.Writes[0].Image;
        Assert.NotNull(written);
        var result = Inspect(written);
        Assert.Equal(SKEncodedImageFormat.Png, result.Format);
        Assert.Equal(1920, result.Width);
        Assert.Equal(1080, result.Height);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }
}
