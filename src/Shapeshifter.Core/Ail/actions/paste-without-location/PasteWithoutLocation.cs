using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Security.Cryptography;
using MetadataExtractor.Formats.Exif;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that pastes a photo without the place it was taken and its other metadata.
/// </summary>
public sealed class PasteWithoutLocationAction : IClipboardAction
{
    private const long MaximumFileBytes = 25L * 1024 * 1024;

    /// <summary>The action's stable identifier.</summary>
    public string Id => "paste-without-location";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 62;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocation;
    }

    /// <summary>Gets the action's description.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteWithoutLocationDescription;
    }

    /// <summary>Tells whether the item is a JPEG, PNG or WebP image that holds a position.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var image = ReadImage(item);
        return image is not null && IsSupportedType(image) && HasLocation(image);
    }

    /// <summary>Pastes the item's image, or a copy of its file, without its metadata.</summary>
    /// <param name="item">The item to paste.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes when the paste was made.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        if (item.Kind == ClipboardContentKind.Image)
        {
            var image = item.Snapshot.Image ?? throw new InvalidOperationException("The item holds no image.");
            await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = Clean(image) }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (item.Kind == ClipboardContentKind.File)
        {
            var source = item.Snapshot.Files[0];
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(false);
            var cleaned = Clean(bytes);
            var folder = Path.Combine(
                context.Paths.TempDirectory,
                "Without location",
                Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4)));
            Directory.CreateDirectory(folder);
            var target = Path.Combine(folder, Path.GetFileName(source));
            await File.WriteAllBytesAsync(target, cleaned, cancellationToken).ConfigureAwait(false);
            await context.WriteAndPasteAsync(new ClipboardSnapshot { Files = [target] }, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("The item is neither an image nor a file.");
    }

    /// <summary>Tells whether the image's metadata holds a position.</summary>
    /// <param name="image">The encoded image.</param>
    /// <returns>True when a GPS position is found; false otherwise or when the image cannot be read.</returns>
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

    /// <summary>Encodes the image again in its own type, turned upright, without any metadata.</summary>
    /// <param name="image">The encoded JPEG, PNG or WebP image.</param>
    /// <returns>The image without EXIF, XMP or IPTC metadata.</returns>
    public static byte[] Clean(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);

        using var data = SKData.CreateCopy(image);
        using var codec = SKCodec.Create(data) ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));

        var (format, quality) = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg => (SKEncodedImageFormat.Jpeg, 95),
            SKEncodedImageFormat.Png => (SKEncodedImageFormat.Png, 100),
            SKEncodedImageFormat.Webp => (SKEncodedImageFormat.Webp, 95),
            _ => throw new ArgumentException("The image is not a JPEG, PNG or WebP.", nameof(image)),
        };

        var origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec) ?? throw new ArgumentException("The image cannot be decoded.", nameof(image));
        using var oriented = origin == SKEncodedOrigin.TopLeft ? null : Orient(decoded, origin);
        var upright = oriented ?? decoded;

        using var skImage = SKImage.FromBitmap(upright);
        using var encoded = skImage.Encode(format, quality) ?? throw new InvalidOperationException("The image cannot be encoded.");
        return encoded.ToArray();
    }

    private static SKBitmap Orient(SKBitmap source, SKEncodedOrigin origin)
    {
        float sw = source.Width;
        float sh = source.Height;
        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;

        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, sw, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, sw, 0, -1, sh, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, sh, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, sh, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, sh, -1, 0, sw, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, sw, 0, 0, 1),
            _ => SKMatrix.Identity,
        };

        var result = new SKBitmap(new SKImageInfo(width, height, source.ColorType, source.AlphaType, source.ColorSpace));
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
            return item.Snapshot.Image;
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
            if (!info.Exists || info.Length > MaximumFileBytes)
            {
                return null;
            }

            return File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;
        }
    }

    private static bool IsSupportedType(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReadOnlySpan<byte> riff = [0x52, 0x49, 0x46, 0x46];
        ReadOnlySpan<byte> webp = [0x57, 0x45, 0x42, 0x50];

        return bytes.StartsWith(jpeg)
            || bytes.StartsWith(png)
            || (bytes.Length >= 12 && bytes.StartsWith(riff) && bytes.Slice(8, 4).SequenceEqual(webp));
    }
}
