using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that unpacks a copied .zip file into Shapeshifter's temporary folder
/// and pastes the files and folders directly inside it, without adding them to the history.
/// </summary>
public sealed class ExtractZipAction : IClipboardAction
{
    /// <summary>The most bytes the entries of an archive may unpack to together (2 GB).</summary>
    public const long MaxUnpackedBytes = 2147483648;

    /// <summary>The stable identifier of this action.</summary>
    public string Id => "extract-zip";

    /// <summary>The position of this action in the list of actions.</summary>
    public int Order => 76;

    /// <summary>Gives the title shown for this action.</summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZip;
    }

    /// <summary>Gives the description shown for this action.</summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionExtractZipDescription;
    }

    /// <summary>Tells whether the item is a single .zip file that can be unpacked.</summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            && item.Snapshot.Files.Count == 1
            && IsZip(item.Snapshot.Files[0]);
    }

    /// <summary>Unpacks the item's archive and pastes its top-level entries as files.</summary>
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
    /// Tells whether the path names an existing local file whose name ends in .zip
    /// and whose first four bytes are a zip signature.
    /// </summary>
    public static bool IsZip(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(path);
            Span<byte> header = stackalloc byte[4];
            int read = 0;
            while (read < 4)
            {
                int n = stream.Read(header[read..]);
                if (n == 0)
                {
                    return false;
                }

                read += n;
            }

            return header[0] == 0x50 && header[1] == 0x4B
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
    /// Unpacks the archive into a new folder under the temporary folder and gives the full
    /// paths of the entries directly in it, ordered by name, ordinal and ignoring case.
    /// </summary>
    public static IReadOnlyList<string> Extract(string zip, string temp)
    {
        ArgumentNullException.ThrowIfNull(zip);
        ArgumentNullException.ThrowIfNull(temp);

        string name = Path.GetFileName(zip);
        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4];
        }

        string folder = Path.GetFullPath(Path.Combine(
            temp,
            "Extracted files",
            RandomNumberGenerator.GetHexString(8, lowercase: true),
            name));
        string prefix = folder.EndsWith(Path.DirectorySeparatorChar)
            ? folder
            : folder + Path.DirectorySeparatorChar;

        ZipArchive archive;
        try
        {
            archive = ZipFile.OpenRead(zip);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("The archive cannot be read.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidDataException("The archive cannot be read.", exception);
        }

        using (archive)
        {
            var targets = new List<(ZipArchiveEntry Entry, string Path)>();
            long total = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (Path.IsPathRooted(entry.FullName))
                {
                    throw new InvalidDataException("An entry of the archive has an absolute path.");
                }

                string target = Path.GetFullPath(Path.Combine(folder, entry.FullName));
                bool inside = string.Equals(target, folder, StringComparison.Ordinal)
                    || target.StartsWith(prefix, StringComparison.Ordinal);
                if (!inside)
                {
                    throw new InvalidDataException("An entry of the archive would end up outside its folder.");
                }

                total += entry.Length;
                if (total > MaxUnpackedBytes)
                {
                    throw new InvalidDataException("The archive unpacks to more than the allowed size.");
                }

                targets.Add((entry, target));
            }

            Directory.CreateDirectory(folder);
            foreach ((ZipArchiveEntry entry, string target) in targets)
            {
                bool isDirectory = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
                if (isDirectory)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                string? parent = Path.GetDirectoryName(target);
                if (parent is not null)
                {
                    Directory.CreateDirectory(parent);
                }

                entry.ExtractToFile(target, overwrite: true);
            }
        }

        var result = new List<string>(Directory.EnumerateFileSystemEntries(folder));
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }
}
