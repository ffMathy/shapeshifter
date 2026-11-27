using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes the text inside a copied file instead of the file itself.
/// </summary>
public sealed class PasteFileContentsAction : IClipboardAction
{
    /// <summary>
    /// The largest file, in bytes, whose contents can be pasted.
    /// </summary>
    public const int MaxBytes = 1048576;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly UnicodeEncoding StrictUtf16LittleEndian = new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    private static readonly UnicodeEncoding StrictUtf16BigEndian = new(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "paste-file-contents";

    /// <summary>
    /// The position of this action among the actions.
    /// </summary>
    public int Order => 72;

    /// <summary>
    /// Gives the title shown for this action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileContents;
    }

    /// <summary>
    /// Gives the description shown for this action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteFileContentsDescription;
    }

    /// <summary>
    /// Tells whether the item is one file whose contents can be read as text.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is one readable text file.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            && item.Snapshot.Files is [var path]
            && ReadText(path) is not null;
    }

    /// <summary>
    /// Reads the file again and pastes its text, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    /// <exception cref="InvalidOperationException">The file no longer holds readable text.</exception>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var text = item.Kind == ClipboardContentKind.File && item.Snapshot.Files is [var path]
            ? ReadText(path)
            : null;
        if (text is null)
        {
            throw new InvalidOperationException("The file no longer holds text that can be pasted.");
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = text }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a local file of at most <see cref="MaxBytes"/> bytes as UTF-8, or as UTF-16 when it starts with a UTF-16 byte order mark.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The file's text, unchanged, or null when the file cannot be read as text.</returns>
    public static string? ReadText(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            if (new FileInfo(path).Length > MaxBytes)
            {
                return null;
            }

            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        if (bytes.Length == 0 || bytes.Length > MaxBytes)
        {
            return null;
        }

        string text;
        try
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                text = StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                text = StrictUtf16LittleEndian.GetString(bytes, 2, bytes.Length - 2);
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                text = StrictUtf16BigEndian.GetString(bytes, 2, bytes.Length - 2);
            }
            else
            {
                text = StrictUtf8.GetString(bytes);
            }
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (text.Length == 0)
        {
            return null;
        }

        foreach (var c in text)
        {
            if (c < '\u0020' && c != '\t' && c != '\n' && c != '\r' && c != '\f')
            {
                return null;
            }
        }

        return text;
    }
}
