namespace Shapeshifter.Core.Clipboard;

/// <summary>
/// One entry of a Windows <c>FileGroupDescriptorW</c>: a file or folder that an application offers on the clipboard
/// without it existing on disk, such as a file copied in a Remote Desktop session or an Outlook attachment.
/// </summary>
/// <param name="Name">The relative path, with folders separated by a backslash.</param>
/// <param name="IsDirectory">Whether the entry is a folder (its contents follow as entries of their own).</param>
/// <param name="Size">The size in bytes, when the descriptor states one.</param>
public sealed record VirtualFileDescriptor(string Name, bool IsDirectory, long? Size);
