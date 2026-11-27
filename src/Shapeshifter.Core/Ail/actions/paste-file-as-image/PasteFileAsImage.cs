using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes the picture held in a single copied file as an image instead of as the file.
/// </summary>
public sealed class PasteFileAsImageAction : IClipboardAction
{
    /// <summary>
    /// The largest file, in bytes, that is read as an image (25 MB).
    /// </summary>
    public const int MaxBytes = 26214400;

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-file-as-image";

    /// <summary>
    /// Where the action is listed among the actions.
    /// </summary>
    public int Order => 74;

    /// <summary>
    /// Gets the action's title shown in the actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImage;
    }

    /// <summary>
    /// Gets the action's description shown in the actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImageDescription;
    }

    /// <summary>
    /// Tells whether the item is one file whose contents are an image.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is a single file holding an image.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            && item.Snapshot.Files.Count == 1
            && ReadImage(item.Snapshot.Files[0]) is not null;
    }

    /// <summary>
    /// Reads the file again and pastes its bytes as an image, without adding anything to the history.
    /// </summary>
    /// <param name="item">The item holding the file.</param>
    /// <param name="context">The context used to write the clipboard and paste.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the image has been pasted.</returns>
    /// <exception cref="InvalidOperationException">The file no longer holds an image.</exception>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var bytes = item.Snapshot.Files.Count == 1 ? ReadImage(item.Snapshot.Files[0]) : null;
        if (bytes is null)
        {
            throw new InvalidOperationException();
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = bytes }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a local file's bytes when they start as a PNG, JPEG, GIF, BMP, WebP or TIFF image, whatever the file is called.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>The file's bytes, or null for a network path, a missing file, a file larger than <see cref="MaxBytes"/>, or one that is not an image.</returns>
    public static byte[]? ReadImage(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        if (new FileInfo(path).Length > MaxBytes)
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        return IsImage(bytes) ? bytes : null;
    }

    private static bool IsImage(ReadOnlySpan<byte> bytes)
    {
        return bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A])
            || bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF])
            || bytes.StartsWith((ReadOnlySpan<byte>)[0x47, 0x49, 0x46, 0x38])
            || bytes.StartsWith((ReadOnlySpan<byte>)[0x42, 0x4D])
            || (bytes.Length >= 12
                && bytes.StartsWith((ReadOnlySpan<byte>)[0x52, 0x49, 0x46, 0x46])
                && bytes.Slice(8, 4).SequenceEqual((ReadOnlySpan<byte>)[0x57, 0x45, 0x42, 0x50]))
            || bytes.StartsWith((ReadOnlySpan<byte>)[0x49, 0x49, 0x2A, 0x00])
            || bytes.StartsWith((ReadOnlySpan<byte>)[0x4D, 0x4D, 0x00, 0x2A]);
    }
}
