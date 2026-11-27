using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes the picture held in a copied image file instead of the file itself.
/// </summary>
public sealed class PasteFileAsImageAction : IClipboardAction
{
    /// <summary>
    /// The largest file, in bytes, that is pasted as an image (25 MB).
    /// </summary>
    public const int MaxBytes = 26214400;

    private static readonly byte[] PngStart = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegStart = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] GifStart = [0x47, 0x49, 0x46, 0x38];
    private static readonly byte[] BmpStart = [0x42, 0x4D];
    private static readonly byte[] RiffStart = [0x52, 0x49, 0x46, 0x46];
    private static readonly byte[] WebpMark = [0x57, 0x45, 0x42, 0x50];
    private static readonly byte[] TiffLittleStart = [0x49, 0x49, 0x2A, 0x00];
    private static readonly byte[] TiffBigStart = [0x4D, 0x4D, 0x00, 0x2A];

    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "paste-file-as-image";

    /// <summary>
    /// The position of this action among the actions.
    /// </summary>
    public int Order => 74;

    /// <summary>
    /// Gets the title shown for this action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImage;
    }

    /// <summary>
    /// Gets the description shown for this action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImageDescription;
    }

    /// <summary>
    /// Tells whether the item is one file whose contents are an image.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is one file holding an image.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.File || item.Snapshot.Files.Count != 1)
        {
            return false;
        }

        return ReadImage(item.Snapshot.Files[0]) is not null;
    }

    /// <summary>
    /// Reads the file again and pastes its picture, without adding it to the history.
    /// </summary>
    /// <param name="item">The file item to paste as an image.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the image is pasted.</returns>
    /// <exception cref="System.InvalidOperationException">The file no longer holds an image.</exception>
    public async System.Threading.Tasks.Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(context);

        byte[]? image = item.Snapshot.Files.Count == 1 ? ReadImage(item.Snapshot.Files[0]) : null;
        if (image is null)
        {
            throw new System.InvalidOperationException();
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = image }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a local file's bytes when they start as a PNG, JPEG, GIF, BMP, WebP or TIFF image and are at most <see cref="MaxBytes"/> long.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The file's bytes, or null when it is not such an image.</returns>
    public static byte[]? ReadImage(string path)
    {
        System.ArgumentNullException.ThrowIfNull(path);

        if (path.StartsWith(@"\\", System.StringComparison.Ordinal) || path.StartsWith("//", System.StringComparison.Ordinal))
        {
            return null;
        }

        if (!System.IO.File.Exists(path))
        {
            return null;
        }

        if (new System.IO.FileInfo(path).Length > MaxBytes)
        {
            return null;
        }

        byte[] bytes = System.IO.File.ReadAllBytes(path);
        if (bytes.Length > MaxBytes)
        {
            return null;
        }

        return IsImage(bytes) ? bytes : null;
    }

    private static bool IsImage(byte[] bytes)
    {
        return StartsWith(bytes, PngStart, 0)
            || StartsWith(bytes, JpegStart, 0)
            || StartsWith(bytes, GifStart, 0)
            || StartsWith(bytes, BmpStart, 0)
            || (StartsWith(bytes, RiffStart, 0) && StartsWith(bytes, WebpMark, 8))
            || StartsWith(bytes, TiffLittleStart, 0)
            || StartsWith(bytes, TiffBigStart, 0);
    }

    private static bool StartsWith(byte[] bytes, byte[] start, int offset)
    {
        if (bytes.Length < offset + start.Length)
        {
            return false;
        }

        for (int i = 0; i < start.Length; i++)
        {
            if (bytes[offset + i] != start[i])
            {
                return false;
            }
        }

        return true;
    }
}
