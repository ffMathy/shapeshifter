using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes a large image scaled down so its longest side is at most <see cref="MaxSide"/> pixels.
/// </summary>
public sealed class PasteSmallerImageAction : IClipboardAction
{
    /// <summary>
    /// The most pixels the longest side of a pasted smaller image may have.
    /// </summary>
    public const int MaxSide = 1920;

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "paste-smaller-image";

    /// <summary>
    /// Where the action sits among the actions.
    /// </summary>
    public int Order => 60;

    /// <summary>
    /// Gets the action's title.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteSmallerImage;
    }

    /// <summary>
    /// Gets the action's description, which names the size the image will be pasted at.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var size = Measure(item.Snapshot.Image)
            ?? throw new ArgumentException("The item does not hold a readable image.", nameof(item));
        var newSize = NewSize(size.Width, size.Height);
        return Localizer.Format(Strings.ActionPasteSmallerImageDescription, newSize.Width, newSize.Height);
    }

    /// <summary>
    /// Tells whether the item is an image whose longest side is more than <see cref="MaxSide"/> pixels.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action can scale the item down.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Image
            && Measure(item.Snapshot.Image) is { } size
            && Math.Max(size.Width, size.Height) > MaxSide;
    }

    /// <summary>
    /// Pastes the item's image scaled down, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes once the image is pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = new ClipboardSnapshot { Image = Shrink(item.Snapshot.Image!) };
        return context.WriteAndPasteAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Reads an image's size from its header, without decoding its pixels.
    /// </summary>
    /// <param name="image">The encoded image, or null.</param>
    /// <returns>The image's size, or null when it cannot be read.</returns>
    public static (int Width, int Height)? Measure(byte[]? image)
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
        return (info.Width, info.Height);
    }

    /// <summary>
    /// Works out the size an image is scaled to so its longest side is <see cref="MaxSide"/> pixels.
    /// </summary>
    /// <param name="width">The image's width in pixels.</param>
    /// <param name="height">The image's height in pixels.</param>
    /// <returns>The scaled size.</returns>
    public static (int Width, int Height) NewSize(int width, int height)
    {
        if (width >= height)
        {
            return (MaxSide, Scale(height, width));
        }

        return (Scale(width, height), MaxSide);
    }

    /// <summary>
    /// Decodes an image, turns it the right way up, scales it to <see cref="NewSize"/> and encodes it in its own type.
    /// </summary>
    /// <param name="image">The encoded image.</param>
    /// <returns>The scaled image, as JPEG for a JPEG and as PNG otherwise.</returns>
    public static byte[] Shrink(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data)
            ?? throw new ArgumentException("The image cannot be read.", nameof(image));
        using var decoded = SKBitmap.Decode(codec);
        using var oriented = Orient(decoded, codec.EncodedOrigin);

        var size = NewSize(oriented.Width, oriented.Height);
        var info = new SKImageInfo(size.Width, size.Height, oriented.ColorType, oriented.AlphaType);
        using var resized = oriented.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));

        var isJpeg = codec.EncodedFormat == SKEncodedImageFormat.Jpeg;
        using var encoded = isJpeg
            ? resized.Encode(SKEncodedImageFormat.Jpeg, 85)
            : resized.Encode(SKEncodedImageFormat.Png, 100);
        return encoded.ToArray();
    }

    private static int Scale(int side, int longest)
    {
        var scaled = ((long)side * MaxSide * 2 + longest) / (2L * longest);
        return (int)Math.Max(1L, scaled);
    }

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        float w = bitmap.Width;
        float h = bitmap.Height;
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        var matrix = origin switch
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

        var width = swaps ? bitmap.Height : bitmap.Width;
        var height = swaps ? bitmap.Width : bitmap.Height;
        var result = new SKBitmap(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return result;
    }
}
