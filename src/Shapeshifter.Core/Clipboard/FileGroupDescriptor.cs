using System.Buffers.Binary;
using System.Text;

namespace Shapeshifter.Core.Clipboard;

/// <summary>
/// Reads the Windows <c>FILEGROUPDESCRIPTORW</c> structure: a count followed by that many <c>FILEDESCRIPTORW</c>s.
/// </summary>
/// <remarks>
/// See https://learn.microsoft.com/windows/win32/api/shlobj_core/ns-shlobj_core-filedescriptorw. The layout is the same
/// in 32- and 64-bit processes: it holds no pointers.
/// </remarks>
public static class FileGroupDescriptor
{
    /// <summary>The clipboard format that lists the virtual files.</summary>
    public const string FormatName = "FileGroupDescriptorW";

    /// <summary>The ANSI variant of <see cref="FormatName"/>, offered next to it by older applications.</summary>
    public const string AnsiFormatName = "FileGroupDescriptor";

    /// <summary>The clipboard format that holds the contents of each virtual file, read one index at a time.</summary>
    public const string ContentsFormatName = "FileContents";

    /// <summary>The size of one <c>FILEDESCRIPTORW</c>.</summary>
    public const int DescriptorSize = 592;

    private const int MaxPath = 260;
    private const uint HasAttributes = 0x00000004;   // FD_ATTRIBUTES
    private const uint HasFileSize = 0x00000040;     // FD_FILESIZE
    private const uint DirectoryAttribute = 0x00000010; // FILE_ATTRIBUTE_DIRECTORY

    private const int FlagsOffset = 0;
    private const int AttributesOffset = 36;
    private const int FileSizeHighOffset = 64;
    private const int FileSizeLowOffset = 68;
    private const int FileNameOffset = 72;

    /// <summary>
    /// The entries of a <c>FILEGROUPDESCRIPTORW</c>. Entries beyond the end of <paramref name="data"/> and entries
    /// without a name are left out.
    /// </summary>
    public static IReadOnlyList<VirtualFileDescriptor> Parse(ReadOnlySpan<byte> data)
    {
        if (data.Length < sizeof(uint))
        {
            return [];
        }

        var count = BinaryPrimitives.ReadUInt32LittleEndian(data);
        var available = (data.Length - sizeof(uint)) / DescriptorSize;
        var entries = new List<VirtualFileDescriptor>((int)Math.Min(count, (uint)available));
        for (var index = 0; index < count && index < available; index++)
        {
            var entry = data.Slice(sizeof(uint) + (index * DescriptorSize), DescriptorSize);
            var flags = BinaryPrimitives.ReadUInt32LittleEndian(entry[FlagsOffset..]);
            var attributes = BinaryPrimitives.ReadUInt32LittleEndian(entry[AttributesOffset..]);
            var isDirectory = (flags & HasAttributes) != 0 && (attributes & DirectoryAttribute) != 0;
            long? size = (flags & HasFileSize) != 0
                ? ((long)BinaryPrimitives.ReadUInt32LittleEndian(entry[FileSizeHighOffset..]) << 32)
                  | BinaryPrimitives.ReadUInt32LittleEndian(entry[FileSizeLowOffset..])
                : null;

            var nameBytes = entry.Slice(FileNameOffset, MaxPath * sizeof(char));
            var name = Encoding.Unicode.GetString(nameBytes);
            var end = name.IndexOf('\0', StringComparison.Ordinal);
            name = end >= 0 ? name[..end] : name;
            entries.Add(new VirtualFileDescriptor(name, isDirectory, size));
        }

        return entries;
    }

    /// <summary>Builds a <c>FILEGROUPDESCRIPTORW</c>; the inverse of <see cref="Parse"/>, for tests and tools.</summary>
    public static byte[] Create(IReadOnlyList<VirtualFileDescriptor> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var data = new byte[sizeof(uint) + (entries.Count * DescriptorSize)];
        BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)entries.Count);
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = data.AsSpan(sizeof(uint) + (index * DescriptorSize), DescriptorSize);
            var descriptor = entries[index];
            var flags = HasAttributes | (descriptor.Size is null ? 0 : HasFileSize);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[FlagsOffset..], flags);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[AttributesOffset..], descriptor.IsDirectory ? DirectoryAttribute : 0);
            if (descriptor.Size is { } size)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(entry[FileSizeHighOffset..], (uint)(size >> 32));
                BinaryPrimitives.WriteUInt32LittleEndian(entry[FileSizeLowOffset..], (uint)size);
            }

            var name = descriptor.Name.Length < MaxPath ? descriptor.Name : descriptor.Name[..(MaxPath - 1)];
            Encoding.Unicode.GetBytes(name, entry.Slice(FileNameOffset, MaxPath * sizeof(char)));
        }

        return data;
    }
}
