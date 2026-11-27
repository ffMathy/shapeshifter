using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Pastes a photo without the place it was taken and its other metadata, re-encoding the image with its orientation applied.
/// </summary>
public sealed class PasteWithoutLocationAction : IClipboardAction
{
    private const long MaxFileBytes = 25L * 1024 * 1024;
    private const int Quality = 95;

    /// <summary>The action's stable identifier.</summary>
    public string Id => "paste-without-location";

    /// <summary>The action's position among the actions.</summary>
    public int Order => 62;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocation;
    }

    /// <summary>Gets the action's description.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocationDescription;
    }

    /// <summary>Tells whether the item is a JPEG, PNG or WebP image, or a single such file, that holds a position.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var image = ReadImage(item);
        return image is not null && HasLocation(image);
    }

    /// <summary>Pastes the item's image, or a new copy of its file, with all metadata removed; nothing is added to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action may use.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes once the cleaned image has been pasted.</returns>
    public async System.Threading.Tasks.Task ExecuteAsync(ClipboardItem item, ActionContext context, System.Threading.CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var image = ReadImage(item) ?? throw new InvalidOperationException("The item holds no supported image.");
        var cleaned = Clean(image);
        if (item.Kind == ClipboardContentKind.Image)
        {
            await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = cleaned }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var name = System.IO.Path.GetFileName(item.Snapshot.Files[0]);
        var folder = System.IO.Path.Combine(
            context.Paths.TempDirectory,
            "Without location",
            Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)));
        System.IO.Directory.CreateDirectory(folder);
        var target = System.IO.Path.Combine(folder, name);
        await System.IO.File.WriteAllBytesAsync(target, cleaned, cancellationToken).ConfigureAwait(false);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Files = [target] }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells whether the image's metadata holds a GPS position.</summary>
    /// <param name="image">The encoded image.</param>
    /// <returns>True when a position is found; false otherwise or when the image cannot be read.</returns>
    public static bool HasLocation(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        try
        {
            using var stream = new System.IO.MemoryStream(image, writable: false);
            foreach (var directory in ImageMetadataReader.ReadMetadata(stream))
            {
                if (directory is GpsDirectory gps && gps.GetGeoLocation() is not null)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Decodes the image, applies its EXIF orientation and encodes it again in its own type, without any metadata.</summary>
    /// <param name="image">The encoded JPEG, PNG or WebP image.</param>
    /// <returns>The re-encoded image.</returns>
    public static byte[] Clean(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        using var stream = new System.IO.MemoryStream(image, writable: false);
        using var codec = SKCodec.Create(stream) ?? throw new ArgumentException("The bytes are not a decodable image.", nameof(image));
        var format = codec.EncodedFormat;
        var quality = format switch
        {
            SKEncodedImageFormat.Jpeg => Quality,
            SKEncodedImageFormat.Webp => Quality,
            SKEncodedImageFormat.Png => 100,
            _ => throw new ArgumentException("The image is not a JPEG, PNG or WebP.", nameof(image)),
        };
        var origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec) ?? throw new ArgumentException("The bytes are not a decodable image.", nameof(image));
        if (origin == SKEncodedOrigin.TopLeft)
        {
            return Encode(decoded, format, quality);
        }

        using var oriented = Orient(decoded, origin);
        return Encode(oriented, format, quality);
    }

    private static byte[] Encode(SKBitmap bitmap, SKEncodedImageFormat format, int quality)
    {
        using var data = bitmap.Encode(format, quality) ?? throw new InvalidOperationException("The image could not be encoded.");
        return data.ToArray();
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width;
        float h = source.Height;
        var swaps = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
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
        var info = new SKImageInfo(
            swaps ? source.Height : source.Width,
            swaps ? source.Width : source.Height,
            source.ColorType,
            source.AlphaType,
            source.ColorSpace);
        var result = new SKBitmap(info);
        using var canvas = new SKCanvas(result);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return result;
    }

    private static byte[]? ReadImage(ClipboardItem item)
    {
        if (item.Kind == ClipboardContentKind.Image)
        {
            var image = item.Snapshot.Image;
            return image is not null && IsSupportedType(image) ? image : null;
        }

        if (item.Kind != ClipboardContentKind.File || item.Snapshot.Files.Count != 1)
        {
            return null;
        }

        var path = item.Snapshot.Files[0];
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var info = new System.IO.FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            var bytes = System.IO.File.ReadAllBytes(path);
            return IsSupportedType(bytes) ? bytes : null;
        }
        catch (System.IO.IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsSupportedType(byte[] bytes)
    {
        var isJpeg = bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
        var isPng = bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47
            && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
        var isWebp = bytes.Length >= 12
            && bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F'
            && bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
        return isJpeg || isPng || isWebp;
    }
}
