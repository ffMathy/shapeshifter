using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes an image scaled down so that its longest side is at most <see cref="MaxSide"/> pixels.
/// </summary>
public sealed class PasteSmallerImageAction : IClipboardAction
{
    /// <summary>The most pixels the longest side of a pasted smaller image has.</summary>
    public const int MaxSide = 1920;

    /// <summary>The action's unique id.</summary>
    public string Id => "paste-smaller-image";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 60;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteSmallerImage;
    }

    /// <summary>Gets the action's description, which gives the size the image is pasted at.</summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var size = OrientedSize(item.Snapshot.Image)
            ?? throw new ArgumentException("The item has no image that can be read.", nameof(item));
        var newSize = NewSize(size.Width, size.Height);
        return Localizer.Format(Strings.ActionPasteSmallerImageDescription, newSize.Width, newSize.Height);
    }

    /// <summary>Tells whether the item is an image whose longest side is more than <see cref="MaxSide"/>.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Image)
        {
            return false;
        }

        var size = Measure(item.Snapshot.Image);
        return size is { } s && Math.Max(s.Width, s.Height) > MaxSide;
    }

    /// <summary>Pastes the item's image scaled down, without adding it to the history.</summary>
    /// <param name="item">The item whose image is pasted.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the image has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var image = item.Snapshot.Image
            ?? throw new ArgumentException("The item has no image.", nameof(item));
        var snapshot = new ClipboardSnapshot { Image = Shrink(image) };
        return context.WriteAndPasteAsync(snapshot, cancellationToken);
    }

    /// <summary>Reads an image's size from its header, without decoding its pixels.</summary>
    /// <param name="image">The encoded image, or null.</param>
    /// <returns>The image's size, or null when it cannot be read.</returns>
    public static (int Width, int Height)? Measure(byte[]? image)
    {
        var header = ReadHeader(image);
        return header is { } h ? (h.Width, h.Height) : null;
    }

    /// <summary>Works out the size an image is scaled to so that its longest side is <see cref="MaxSide"/>.</summary>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <returns>The scaled size; neither side is below 1.</returns>
    public static (int Width, int Height) NewSize(int width, int height)
    {
        if (width >= height)
        {
            return (MaxSide, ScaleSide(height, width));
        }

        return (ScaleSide(width, height), MaxSide);
    }

    /// <summary>Scales an image down to <see cref="NewSize"/>, the right way up, and encodes it in its own type.</summary>
    /// <param name="image">The encoded image.</param>
    /// <returns>The scaled image: a JPEG at quality 85 for a JPEG, else a PNG.</returns>
    public static byte[] Shrink(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data)
            ?? throw new ArgumentException("The image cannot be read.", nameof(image));
        var isJpeg = codec.EncodedFormat == SKEncodedImageFormat.Jpeg;
        var origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec)
            ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));
        using var oriented = Orient(decoded, origin);
        var newSize = NewSize(oriented.Width, oriented.Height);
        using var resized = oriented.Resize(
            oriented.Info.WithSize(newSize.Width, newSize.Height),
            new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new ArgumentException("The image cannot be scaled.", nameof(image));
        using var encoded = isJpeg
            ? resized.Encode(SKEncodedImageFormat.Jpeg, 85)
            : resized.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static int ScaleSide(int side, int longest)
    {
        var scaled = ((2L * side * MaxSide) + longest) / (2L * longest);
        return (int)Math.Max(1L, scaled);
    }

    private static (int Width, int Height, SKEncodedOrigin Origin)? ReadHeader(byte[]? image)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        using var data = SKData.CreateCopy(image);
        if (data is null)
        {
            return null;
        }

        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        var info = codec.Info;
        return (info.Width, info.Height, codec.EncodedOrigin);
    }

    private static (int Width, int Height)? OrientedSize(byte[]? image)
    {
        var header = ReadHeader(image);
        if (header is not { } h)
        {
            return null;
        }

        return SwapsSides(h.Origin) ? (h.Height, h.Width) : (h.Width, h.Height);
    }

    private static bool SwapsSides(SKEncodedOrigin origin) =>
        origin is SKEncodedOrigin.LeftTop
            or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom
            or SKEncodedOrigin.LeftBottom;

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        float w = bitmap.Width;
        float h = bitmap.Height;
        SKMatrix matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var swap = SwapsSides(origin);
        var info = bitmap.Info.WithSize(swap ? bitmap.Height : bitmap.Width, swap ? bitmap.Width : bitmap.Height);
        var result = new SKBitmap(info);
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return result;
    }
}
