using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes the picture held in a single copied file as an image instead of as the file,
/// for apps that take a pasted picture but not a pasted file.
/// </summary>
public sealed class PasteFileAsImageAction : IClipboardAction
{
    /// <summary>The largest file, in bytes, that is pasted as an image (25 MB).</summary>
    public const int MaxBytes = 26214400;

    /// <summary>The action's stable identifier.</summary>
    public string Id => "paste-file-as-image";

    /// <summary>The action's position among the actions.</summary>
    public int Order => 74;

    /// <summary>Gets the action's title shown in the actions list.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImage;
    }

    /// <summary>Gets the action's description shown in the actions list.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileAsImageDescription;
    }

    /// <summary>Tells whether the item is one file whose contents are an image that can be pasted.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns><see langword="true"/> when the item is a single file holding an image.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File && ReadSingleFile(item) is not null;
    }

    /// <summary>Reads the item's file again and pastes its bytes as an image, without adding to the history.</summary>
    /// <param name="item">The item whose file is pasted.</param>
    /// <param name="context">The context used to write the clipboard and paste.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the image has been pasted.</returns>
    /// <exception cref="InvalidOperationException">The file no longer holds an image that can be pasted.</exception>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var bytes = ReadSingleFile(item) ?? throw new InvalidOperationException();
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Image = bytes }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads a file's bytes when its first bytes show it is an image type the clipboard can carry.</summary>
    /// <param name="path">The path of the file to read.</param>
    /// <returns>The file's bytes, or <see langword="null"/> when it is a network path, not an existing file, larger than <see cref="MaxBytes"/>, or not an image.</returns>
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
        if (bytes.Length > MaxBytes)
        {
            return null;
        }

        return IsImage(bytes) ? bytes : null;
    }

    private static byte[]? ReadSingleFile(ClipboardItem item)
    {
        var files = item.Snapshot.Files;
        return files.Count == 1 ? ReadImage(files[0]) : null;
    }

    private static bool IsImage(ReadOnlySpan<byte> bytes)
    {
        ReadOnlySpan<byte> png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        ReadOnlySpan<byte> jpeg = [0xFF, 0xD8, 0xFF];
        ReadOnlySpan<byte> gif = [0x47, 0x49, 0x46, 0x38];
        ReadOnlySpan<byte> bmp = [0x42, 0x4D];
        ReadOnlySpan<byte> riff = [0x52, 0x49, 0x46, 0x46];
        ReadOnlySpan<byte> webp = [0x57, 0x45, 0x42, 0x50];
        ReadOnlySpan<byte> tiffLittle = [0x49, 0x49, 0x2A, 0x00];
        ReadOnlySpan<byte> tiffBig = [0x4D, 0x4D, 0x00, 0x2A];

        return bytes.StartsWith(png)
            || bytes.StartsWith(jpeg)
            || bytes.StartsWith(gif)
            || bytes.StartsWith(bmp)
            || (bytes.Length >= 12 && bytes.StartsWith(riff) && bytes.Slice(8, 4).SequenceEqual(webp))
            || bytes.StartsWith(tiffLittle)
            || bytes.StartsWith(tiffBig);
    }
}
