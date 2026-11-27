using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that unpacks a copied .zip file and pastes the files inside it.
/// </summary>
public sealed class ExtractZipAction : IClipboardAction
{
    /// <summary>
    /// The most bytes the entries of an archive may unpack to together (2 GB).
    /// </summary>
    public const long MaxUnpackedBytes = 2147483648;

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "extract-zip";

    /// <summary>
    /// The action's position among the actions.
    /// </summary>
    public int Order => 76;

    /// <summary>
    /// Gives the action's title for the item.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZip;
    }

    /// <summary>
    /// Gives the action's description for the item.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZipDescription;
    }

    /// <summary>
    /// Tells whether the item is a single .zip file this action can unpack.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is one .zip file.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            && item.Snapshot.Files is [var path]
            && IsZip(path);
    }

    /// <summary>
    /// Unpacks the item's archive into the temporary folder and pastes its top-level entries.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The action's context.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paste is done.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (item.Snapshot.Files is not [var path])
        {
            return;
        }

        var files = Extract(path, context.Paths.TempDirectory);
        if (files.Count == 0)
        {
            return;
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Files = [.. files] }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether the path names an existing local .zip archive, by its name and its first bytes.
    /// </summary>
    /// <param name="path">The file's path.</param>
    /// <returns>True when the path is a local .zip archive.</returns>
    public static bool IsZip(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (!Path.GetFileName(path).EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            var header = new byte[4];
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (stream.ReadAtLeast(header, 4, throwOnEndOfStream: false) < 4)
            {
                return false;
            }

            return header[0] == 0x50
                && header[1] == 0x4B
                && ((header[2] == 0x03 && header[3] == 0x04) || (header[2] == 0x05 && header[3] == 0x06));
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Unpacks the archive into a new folder in the temporary folder and gives its top-level entries.
    /// </summary>
    /// <param name="zip">The archive's path.</param>
    /// <param name="temp">The temporary folder.</param>
    /// <returns>The full paths of the entries directly in the new folder, ordered by name.</returns>
    /// <exception cref="InvalidDataException">The archive is unreadable, unsafe or too large.</exception>
    public static IReadOnlyList<string> Extract(string zip, string temp)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(temp);

        var name = Path.GetFileNameWithoutExtension(zip);
        var random = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(4));
        var destination = Path.GetFullPath(Path.Combine(temp, "Extracted files", random, name));
        var prefix = destination.EndsWith(Path.DirectorySeparatorChar)
            ? destination
            : destination + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(zip);

        long total = 0;
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destination, entry.FullName));
            if (!target.StartsWith(prefix, StringComparison.Ordinal) && target != destination)
            {
                throw new InvalidDataException("The archive has an entry outside its folder.");
            }

            total += entry.Length;
            if (total > MaxUnpackedBytes)
            {
                throw new InvalidDataException("The archive unpacks to more than the allowed size.");
            }
        }

        Directory.CreateDirectory(destination);
        archive.ExtractToDirectory(destination);

        return [.. Directory.EnumerateFileSystemEntries(destination).OrderBy(path => path, StringComparer.OrdinalIgnoreCase)];
    }
}
