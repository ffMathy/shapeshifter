// ail-derived-from: OpenQrLink@2582f0553e0510e2
using System.Runtime.InteropServices;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using SkiaSharp;
using Xunit;
using ZXing;
using ZXing.Common;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the OpenQrLink node.</summary>
public sealed class OpenQrLinkTests
{
    private static readonly byte[] NotAnImage = [1, 2, 3];

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static byte[] QrImagePng(string contents)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = 300, Height = 300, Margin = 2 },
        };
        var pixelData = writer.Write(contents);

        using var qr = new SKBitmap(pixelData.Width, pixelData.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        Marshal.Copy(pixelData.Pixels, 0, qr.GetPixels(), pixelData.Pixels.Length);

        using var canvasBitmap = new SKBitmap(1200, 800, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(canvasBitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(qr, 450, 250);
            canvas.Flush();
        }

        return EncodePng(canvasBitmap);
    }

    private static byte[] WhitePng(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            canvas.Flush();
        }

        return EncodePng(bitmap);
    }

    [Fact(DisplayName = "example 1: FindLink(a PNG of such an image with the QR code of \"https://example.com/hello?x=1\") => https://example.com/hello?x=1")]
    public void Example1()
    {
        var link = OpenQrLinkAction.FindLink(QrImagePng("https://example.com/hello?x=1"));

        Assert.NotNull(link);
        Assert.Equal(new Uri("https://example.com/hello?x=1"), link);
        Assert.Equal("https://example.com/hello?x=1", link.AbsoluteUri);
    }

    [Fact(DisplayName = "example 2: FindLink(such an image with the QR code of \"http://example.org\") => http://example.org/")]
    public void Example2()
    {
        var link = OpenQrLinkAction.FindLink(QrImagePng("http://example.org"));

        Assert.NotNull(link);
        Assert.Equal(new Uri("http://example.org/"), link);
        Assert.Equal("http://example.org/", link.AbsoluteUri);
    }

    [Fact(DisplayName = "example 3: FindLink(such an image with the QR code of \"WIFI:S:home;T:WPA;P:secret;;\") => nothing; not a link")]
    public void Example3()
    {
        Assert.Null(OpenQrLinkAction.FindLink(QrImagePng("WIFI:S:home;T:WPA;P:secret;;")));
    }

    [Fact(DisplayName = "example 4: FindLink(such an image with the QR code of \"see https://example.com\") => nothing; not only a link")]
    public void Example4()
    {
        Assert.Null(OpenQrLinkAction.FindLink(QrImagePng("see https://example.com")));
    }

    [Fact(DisplayName = "example 5: FindLink(such an image with the QR code of \"ftp://example.com/a\") => nothing; not http or https")]
    public void Example5()
    {
        Assert.Null(OpenQrLinkAction.FindLink(QrImagePng("ftp://example.com/a")));
    }

    [Fact(DisplayName = "example 6: FindLink(a white PNG of 1200 × 800) => nothing; no QR code")]
    public void Example6()
    {
        Assert.Null(OpenQrLinkAction.FindLink(WhitePng(1200, 800)));
    }

    [Fact(DisplayName = "example 7: FindLink(a PNG of 5000 × 10) => nothing; longer than MaxSide")]
    public void Example7()
    {
        Assert.Null(OpenQrLinkAction.FindLink(WhitePng(5000, 10)));
    }

    [Fact(DisplayName = "example 8: FindLink([1, 2, 3]) => nothing")]
    public void Example8()
    {
        Assert.Null(OpenQrLinkAction.FindLink(NotAnImage));
    }

    [Fact(DisplayName = "example 9: Shorten(\"https://example.com/a\") => \"https://example.com/a\"")]
    public void Example9()
    {
        Assert.Equal("https://example.com/a", OpenQrLinkAction.Shorten(new Uri("https://example.com/a")));
    }

    [Fact(DisplayName = "example 10: Shorten(\"https://example.com/\" followed by 60 a's) => its first 59 characters, then \"…\"")]
    public void Example10()
    {
        var written = "https://example.com/" + new string('a', 60);

        var shortened = OpenQrLinkAction.Shorten(new Uri(written));

        Assert.Equal(written[..59] + "…", shortened);
    }

    [Fact(DisplayName = "example 11: CanExecute(an Image item of such an image with the QR code of \"https://example.com/hello?x=1\") => true;  GetDescription => \"Opens https://example.com/hello?x=1\"")]
    public void Example11()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = QrImagePng("https://example.com/hello?x=1") });
        var action = new OpenQrLinkAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal("Opens https://example.com/hello?x=1", action.GetDescription(item));
    }

    [Fact(DisplayName = "example 12: CanExecute(text \"https://example.com\") => false")]
    public void Example12()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "https://example.com" });
        var action = new OpenQrLinkAction();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 13: ExecuteAsync(that Image item) => opens https://example.com/hello?x=1; nothing is pasted, the clipboard and the history are unchanged")]
    public async Task Example13()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = QrImagePng("https://example.com/hello?x=1") });
        var action = new OpenQrLinkAction();
        var writesBefore = harness.Backend.Writes.Count;

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Launcher.OpenedUrls);
        Assert.Equal(new Uri("https://example.com/hello?x=1"), harness.Launcher.OpenedUrls[0]);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Equal(writesBefore, harness.Backend.Writes.Count);
        Assert.Single(harness.History.Items);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
