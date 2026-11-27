using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Security.Cryptography;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes a photo without the place it was taken and its other metadata.
/// </summary>
public sealed class PasteWithoutLocationAction : IClipboardAction
{
    private const long MaxFileBytes = 25L * 1024 * 1024;
    private const int Quality = 95;
    private const string FolderName = "Without location";

    /// <summary>The action's unique id.</summary>
    public string Id => "paste-without-location";

    /// <summary>The action's place in the list of actions.</summary>
    public int Order => 62;

    /// <summary>Gets the action's title.</summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocation;
    }

    /// <summary>Gets the action's description.</summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocationDescription;
    }

    /// <summary>Tells whether the item is a JPEG, PNG or WebP image that holds a position.</summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var bytes = ReadImageBytes(item);
        return bytes is not null && HasLocation(bytes);
    }

    /// <summary>Pastes the item's image, or a cleaned copy of its file, without any metadata.</summary>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var bytes = ReadImageBytes(item)
            ?? throw new InvalidOperationException("The item holds no supported image.");
        var cleaned = Clean(bytes);

        if (item.Kind == ClipboardContentKind.Image)
        {
            await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = cleaned }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var source = item.Snapshot.Files[0];
        var folder = Path.Combine(
            context.Paths.TempDirectory,
            FolderName,
            RandomNumberGenerator.GetHexString(8, lowercase: true));
        System.IO.Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, Path.GetFileName(source));
        await File.WriteAllBytesAsync(target, cleaned, cancellationToken).ConfigureAwait(false);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Files = [target] }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Tells whether the image holds a GPS position; any error while reading gives false.</summary>
    public static bool HasLocation(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        try
        {
            using var stream = new MemoryStream(image, writable: false);
            var directories = MetadataExtractor.ImageMetadataReader.ReadMetadata(stream);
            foreach (var directory in directories)
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

    /// <summary>Encodes the image again in its own type, turned as its orientation says, without any metadata.</summary>
    public static byte[] Clean(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data)
            ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));

        var format = codec.EncodedFormat;
        var quality = format switch
        {
            SKEncodedImageFormat.Jpeg => Quality,
            SKEncodedImageFormat.Webp => Quality,
            SKEncodedImageFormat.Png => 100,
            _ => throw new ArgumentException("The image is not a JPEG, PNG or WebP.", nameof(image)),
        };

        using var decoded = SKBitmap.Decode(codec)
            ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));
        using var oriented = Orient(decoded, codec.EncodedOrigin);
        using var encoded = oriented.Encode(format, quality)
            ?? throw new InvalidOperationException("The image cannot be encoded.");
        return encoded.ToArray();
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float w = source.Width;
        float h = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;

        // x' = scaleX * x + skewX * y + transX; y' = skewY * x + scaleY * y + transY
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

        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var result = new SKBitmap(source.Info.WithSize(width, height));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.SetMatrix(matrix);
        canvas.DrawBitmap(source, 0, 0);
        canvas.Flush();
        return result;
    }

    private static byte[]? ReadImageBytes(ClipboardItem item)
    {
        if (item.Kind == ClipboardContentKind.Image)
        {
            var image = item.Snapshot.Image;
            return image is not null && IsSupportedImage(image) ? image : null;
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
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > MaxFileBytes)
            {
                return null;
            }

            var bytes = File.ReadAllBytes(path);
            return IsSupportedImage(bytes) ? bytes : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsSupportedImage(byte[] bytes)
    {
        ReadOnlySpan<byte> span = bytes;
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReadOnlySpan<byte> riff = "RIFF"u8;
        ReadOnlySpan<byte> webp = "WEBP"u8;

        return span.StartsWith(jpeg)
            || span.StartsWith(png)
            || (span.Length >= 12 && span.StartsWith(riff) && span.Slice(8, 4).SequenceEqual(webp));
    }
}
