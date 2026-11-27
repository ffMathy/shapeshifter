using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that pastes a large image scaled down so its longest side is at most <see cref="MaxSide"/> pixels.
/// </summary>
public sealed class PasteSmallerImageAction : IClipboardAction
{
    /// <summary>The largest number of pixels the longest side of a pasted smaller image may have.</summary>
    public const int MaxSide = 1920;

    /// <summary>The action's unique id.</summary>
    public string Id => "paste-smaller-image";

    /// <summary>Where the action appears among the actions.</summary>
    public int Order => 60;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteSmallerImage;
    }

    /// <summary>Gives the action's description, which names the size the image will be pasted at.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        (int Width, int Height) size = Measure(item.Snapshot.Image)
            ?? throw new InvalidOperationException("The image cannot be read.");
        (int Width, int Height) newSize = NewSize(size.Width, size.Height);
        return Localizer.Format(Strings.ActionPasteSmallerImageDescription, newSize.Width, newSize.Height);
    }

    /// <summary>Tells whether the item is an image whose longest side is more than <see cref="MaxSide"/>.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can scale the item down.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Image)
        {
            return false;
        }

        (int Width, int Height)? size = Measure(item.Snapshot.Image);
        return size is { } measured && Math.Max(measured.Width, measured.Height) > MaxSide;
    }

    /// <summary>Pastes the item's image scaled down, without adding it to the history.</summary>
    /// <param name="item">The item whose image is pasted.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the image has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        byte[] image = item.Snapshot.Image ?? throw new InvalidOperationException("The item holds no image.");
        ClipboardSnapshot snapshot = new() { Image = Shrink(image) };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads an image's size from its header, without decoding its pixels.</summary>
    /// <param name="image">The encoded image, or null.</param>
    /// <returns>The image's size, or null when it cannot be read.</returns>
    public static (int Width, int Height)? Measure(byte[]? image)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        using SKData? data = SKData.CreateCopy(image);
        if (data is null)
        {
            return null;
        }

        using SKCodec? codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        SKImageInfo info = codec.Info;
        return (info.Width, info.Height);
    }

    /// <summary>Gives the size whose longest side is <see cref="MaxSide"/>, keeping the proportions.</summary>
    /// <param name="width">The original width.</param>
    /// <param name="height">The original height.</param>
    /// <returns>The new size; the shorter side is rounded half up and never below 1.</returns>
    public static (int Width, int Height) NewSize(int width, int height)
    {
        if (width >= height)
        {
            return (MaxSide, Scale(height, width));
        }

        return (Scale(width, height), MaxSide);
    }

    /// <summary>Decodes an image, applies its EXIF orientation, scales it to <see cref="NewSize"/> and encodes it in its own type.</summary>
    /// <param name="image">The encoded image.</param>
    /// <returns>The scaled image: JPEG at quality 85 for a JPEG, otherwise PNG.</returns>
    public static byte[] Shrink(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using SKData data = SKData.CreateCopy(image)
            ?? throw new ArgumentException("The image cannot be read.", nameof(image));
        using SKCodec codec = SKCodec.Create(data)
            ?? throw new ArgumentException("The image cannot be read.", nameof(image));
        SKEncodedImageFormat format = codec.EncodedFormat == SKEncodedImageFormat.Jpeg
            ? SKEncodedImageFormat.Jpeg
            : SKEncodedImageFormat.Png;
        int quality = format == SKEncodedImageFormat.Jpeg ? 85 : 100;
        SKEncodedOrigin origin = codec.EncodedOrigin;

        using SKBitmap decoded = SKBitmap.Decode(codec)
            ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));
        using SKBitmap oriented = Orient(decoded, origin);
        (int Width, int Height) newSize = NewSize(oriented.Width, oriented.Height);
        SKImageInfo target = new(newSize.Width, newSize.Height, oriented.ColorType, oriented.AlphaType);
        using SKBitmap resized = oriented.Resize(target, new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException("The image cannot be scaled.");
        using SKData encoded = resized.Encode(format, quality)
            ?? throw new InvalidOperationException("The image cannot be encoded.");
        return encoded.ToArray();
    }

    private static int Scale(int side, int longest)
    {
        long scaled = ((2L * side * MaxSide) + longest) / (2L * longest);
        return (int)Math.Max(1L, scaled);
    }

    private static SKBitmap Orient(SKBitmap bitmap, SKEncodedOrigin origin)
    {
        int w = bitmap.Width;
        int h = bitmap.Height;
        SKMatrix matrix;
        bool swapped;
        switch (origin)
        {
            case SKEncodedOrigin.TopRight:
                matrix = new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1);
                swapped = false;
                break;
            case SKEncodedOrigin.BottomRight:
                matrix = new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1);
                swapped = false;
                break;
            case SKEncodedOrigin.BottomLeft:
                matrix = new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1);
                swapped = false;
                break;
            case SKEncodedOrigin.LeftTop:
                matrix = new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1);
                swapped = true;
                break;
            case SKEncodedOrigin.RightTop:
                matrix = new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1);
                swapped = true;
                break;
            case SKEncodedOrigin.RightBottom:
                matrix = new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1);
                swapped = true;
                break;
            case SKEncodedOrigin.LeftBottom:
                matrix = new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1);
                swapped = true;
                break;
            default:
                return bitmap.Copy();
        }

        SKImageInfo info = new(swapped ? h : w, swapped ? w : h, bitmap.ColorType, bitmap.AlphaType);
        SKBitmap oriented = new(info);
        using SKCanvas canvas = new(oriented);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(bitmap, 0, 0);
        canvas.Flush();
        return oriented;
    }
}
