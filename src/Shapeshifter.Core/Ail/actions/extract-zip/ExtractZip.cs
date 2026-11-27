using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

using System.IO.Compression;
using System.Security.Cryptography;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that unpacks a copied .zip file into Shapeshifter's temporary folder and pastes the files inside it.
/// </summary>
public sealed class ExtractZipAction : IClipboardAction
{
    /// <summary>
    /// The most bytes the entries of an archive may unpack to together (2 GB).
    /// </summary>
    public const long MaxUnpackedBytes = 2147483648;

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "extract-zip";

    /// <summary>
    /// Where the action is placed among the actions.
    /// </summary>
    public int Order => 76;

    /// <summary>
    /// Gives the action's title.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZip;
    }

    /// <summary>
    /// Gives the action's description.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZipDescription;
    }

    /// <summary>
    /// Tells whether the item is a single .zip file that can be unpacked.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is one .zip file.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File && IsZip(item.Snapshot.Files[0]);
    }

    /// <summary>
    /// Unpacks the item's archive and pastes its top-level entries, without adding them to the history.
    /// </summary>
    /// <param name="item">The item holding the archive.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the files are pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<string> files = Extract(item.Snapshot.Files[0], context.Paths.TempDirectory);
        if (files.Count == 0)
        {
            return;
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Files = [.. files] }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether a path names an existing local .zip file whose contents start like a zip archive.
    /// </summary>
    /// <param name="path">The path to check.</param>
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

        byte[] header = new byte[4];
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.ReadAtLeast(header, 4, throwOnEndOfStream: false) < 4)
            {
                return false;
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return header[0] == 0x50 && header[1] == 0x4B
            && ((header[2] == 0x03 && header[3] == 0x04) || (header[2] == 0x05 && header[3] == 0x06));
    }

    /// <summary>
    /// Unpacks an archive into a new folder under the temporary folder and gives its top-level entries.
    /// </summary>
    /// <param name="zip">The path of the archive.</param>
    /// <param name="temp">Shapeshifter's temporary folder.</param>
    /// <returns>The full paths of the entries directly in the new folder, ordered by name ignoring case.</returns>
    /// <exception cref="InvalidDataException">The archive cannot be read, has an unsafe entry path, or unpacks to more than <see cref="MaxUnpackedBytes"/>.</exception>
    public static IReadOnlyList<string> Extract(string zip, string temp)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(temp);

        string name = Path.GetFileName(zip);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        string root = Path.GetFullPath(Path.Combine(temp, "Extracted files", RandomNumberGenerator.GetHexString(8, lowercase: true), name));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        using ZipArchive archive = ZipFile.OpenRead(zip);

        long total = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            if (Path.IsPathRooted(entry.FullName))
            {
                throw new InvalidDataException();
            }

            string destination = Path.GetFullPath(Path.Combine(root, entry.FullName));
            if (!string.Equals(destination, root, StringComparison.Ordinal)
                && !destination.StartsWith(rootPrefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException();
            }

            total += entry.Length;
            if (total > MaxUnpackedBytes)
            {
                throw new InvalidDataException();
            }
        }

        Directory.CreateDirectory(root);
        archive.ExtractToDirectory(root);

        string[] entries = Directory.GetFileSystemEntries(root);
        Array.Sort(entries, StringComparer.OrdinalIgnoreCase);
        return entries;
    }
}
