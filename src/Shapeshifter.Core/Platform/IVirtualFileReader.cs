namespace Shapeshifter.Core.Platform;

/// <summary>
/// Reads virtual files from the clipboard: files that the copying application offers as streams rather than as paths,
/// as Remote Desktop does for files copied in a session (Windows: <c>FileGroupDescriptorW</c> and <c>FileContents</c>).
/// </summary>
public interface IVirtualFileReader
{
    /// <summary>
    /// Saves the virtual files on the clipboard into a folder inside <paramref name="parentFolder"/> and returns the
    /// paths of the top-level files and folders; nothing when the clipboard holds none or they could not be read.
    /// </summary>
    Task<IReadOnlyList<string>> ReadAsync(string parentFolder, CancellationToken cancellationToken = default);
}
