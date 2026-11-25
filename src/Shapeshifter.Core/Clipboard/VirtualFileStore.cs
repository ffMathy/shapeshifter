using System.Security.Cryptography;

namespace Shapeshifter.Core.Clipboard;

/// <summary>
/// Turns virtual files (a <c>FileGroupDescriptorW</c> and its <c>FileContents</c> streams) into real files, so that a
/// copy whose files exist only in another process, such as a Remote Desktop session, can be pasted from the history
/// after that process has moved on.
/// </summary>
/// <remarks>
/// The contents stream is only readable while the copying application still owns the clipboard, so the files are read
/// when the copy is captured. Each copy gets a folder named after a hash of its descriptor, which holds the names,
/// sizes and times of its files: copying the same files again finds that folder and reads nothing, and both copies
/// have the same paths, so the history keeps one item.
/// </remarks>
public static class VirtualFileStore
{
    /// <summary>The name of the folder, inside the temporary folder, that holds the copies' folders.</summary>
    public const string FolderName = "Virtual files";

    /// <summary>The most a single copy may hold. Larger copies are not kept.</summary>
    public const long MaxTotalBytes = 1024L * 1024 * 1024;

    private const string PartialSuffix = ".partial";

    private static readonly char[] Separators = ['\\', '/'];

    // Invalid in file names on Windows, where virtual files come from; checked on every system so tests agree.
    private static readonly char[] InvalidNameCharacters = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    /// <summary>
    /// Writes the files that <paramref name="descriptor"/> lists into a folder inside <paramref name="parentFolder"/>
    /// and returns the paths of the top-level files and folders, in the descriptor's order.
    /// </summary>
    /// <param name="parentFolder">Where the copy's folder is created.</param>
    /// <param name="descriptor">The raw <c>FileGroupDescriptorW</c>.</param>
    /// <param name="openContents">
    /// Opens the contents of the file at the given index of the descriptor; <see langword="null"/> when it cannot be read.
    /// </param>
    /// <returns>The top-level paths, or nothing when the copy is empty, too large, or a file could not be read.</returns>
    public static IReadOnlyList<string> Save(
        string parentFolder,
        ReadOnlySpan<byte> descriptor,
        Func<int, Stream?> openContents,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(parentFolder);
        ArgumentNullException.ThrowIfNull(openContents);

        var entries = FileGroupDescriptor.Parse(descriptor);
        var relativePaths = entries.Select(entry => ToRelativePath(entry.Name)).ToArray();
        if (relativePaths.All(path => path is null) || entries.Sum(entry => entry.Size ?? 0) > MaxTotalBytes)
        {
            return [];
        }

        var folder = Path.Combine(parentFolder, Convert.ToHexString(SHA256.HashData(descriptor))[..32]);
        var topLevel = relativePaths
            .OfType<string>()
            .Select(path => path.Split(Path.DirectorySeparatorChar)[0])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => Path.Combine(folder, name))
            .ToArray();
        if (Directory.Exists(folder))
        {
            return topLevel;
        }

        var partial = folder + PartialSuffix;
        try
        {
            if (Directory.Exists(partial))
            {
                Directory.Delete(partial, recursive: true);
            }

            Directory.CreateDirectory(partial);
            long written = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (relativePaths[index] is not { } relative)
                {
                    continue;
                }

                var target = Path.Combine(partial, relative);
                if (entries[index].IsDirectory)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                using var contents = openContents(index);
                if (contents is null)
                {
                    throw new IOException($"The contents of {entries[index].Name} could not be read.");
                }

                written += CopyLimited(contents, target, MaxTotalBytes - written, cancellationToken);
            }

            Directory.Move(partial, folder);
            return topLevel;
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    /// <summary>
    /// A descriptor's name as a path relative to the copy's folder, or <see langword="null"/> when nothing is left of
    /// it. Rooted names, drive letters, <c>.</c> and <c>..</c> are dropped, so no file can land outside the folder.
    /// </summary>
    public static string? ToRelativePath(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var segments = name
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeSegment)
            .Where(segment => segment.Length > 0 && segment is not "." and not "..")
            .ToArray();
        return segments.Length == 0 ? null : Path.Combine(segments);
    }

    private static string SanitizeSegment(string segment)
    {
        var characters = segment
            .Select(character => char.IsControl(character) || InvalidNameCharacters.Contains(character) ? '_' : character)
            .ToArray();

        // Windows drops trailing dots and spaces from names, which would let "..." or ". " stand for a parent folder.
        return new string(characters).Trim().TrimEnd('.');
    }

    private static long CopyLimited(Stream source, string target, long remaining, CancellationToken cancellationToken)
    {
        using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            total += read;
            if (total > remaining)
            {
                throw new IOException("The copied files are larger than Shapeshifter keeps.");
            }

            output.Write(buffer, 0, read);
        }

        return total;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Left for the next start, which empties the temporary folder.
        }
    }
}
