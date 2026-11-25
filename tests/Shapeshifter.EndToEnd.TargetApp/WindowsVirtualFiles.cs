using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Text;

namespace Shapeshifter.EndToEnd.TargetApp;

/// <summary>
/// Puts files on the clipboard the way Remote Desktop does for files copied in a session (issue #171): an OLE data
/// object that lists them in <c>FileGroupDescriptorW</c> and serves each one's contents as an <c>IStream</c> under
/// <c>FileContents</c>, on request, with no file on disk and no <c>CF_HDROP</c>. The contents can only be read while
/// this data object is on the clipboard.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class WindowsVirtualFiles
{
    private const int DataFormatInvalid = unchecked((int)0x80040064);      // DV_E_FORMATETC
    private const int DataIndexInvalid = unchecked((int)0x80040068);       // DV_E_LINDEX
    private const int NotImplemented = unchecked((int)0x80004001);         // E_NOTIMPL
    private const int AdviseNotSupported = unchecked((int)0x80040003);     // OLE_E_ADVISENOTSUPPORTED
    private const int SameFormat = 0x00040130;                             // DATA_S_SAMEFORMATETC

    // The data object stays reachable while it is on the clipboard.
    private static VirtualFileDataObject? current;

    /// <summary>Replaces the clipboard with the files <paramref name="files"/> (name and contents), as virtual files.</summary>
    public static void Copy(IReadOnlyList<(string Name, byte[] Contents)> files)
    {
        current = new VirtualFileDataObject(files);
        var result = OleSetClipboard(current);
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    /// <summary>A <c>FILEGROUPDESCRIPTORW</c> with the name and size of each file (592 bytes per <c>FILEDESCRIPTORW</c>).</summary>
    private static byte[] CreateDescriptor(IReadOnlyList<(string Name, byte[] Contents)> files)
    {
        const int size = 592;
        var data = new byte[4 + files.Count * size];
        BinaryPrimitives.WriteInt32LittleEndian(data, files.Count);
        for (var index = 0; index < files.Count; index++)
        {
            var entry = data.AsSpan(4 + index * size, size);
            BinaryPrimitives.WriteUInt32LittleEndian(entry, 0x40 | 0x04);                   // FD_FILESIZE | FD_ATTRIBUTES
            BinaryPrimitives.WriteUInt32LittleEndian(entry[36..], 0x80);                    // FILE_ATTRIBUTE_NORMAL
            BinaryPrimitives.WriteUInt32LittleEndian(entry[68..], (uint)files[index].Contents.Length);
            Encoding.Unicode.GetBytes(files[index].Name, entry[72..]);
        }

        return data;
    }

    private static nint ToGlobal(byte[] data)
    {
        var memory = GlobalAlloc(0x0002, (nuint)data.Length);   // GMEM_MOVEABLE
        var pointer = GlobalLock(memory);
        Marshal.Copy(data, 0, pointer, data.Length);
        GlobalUnlock(memory);
        return memory;
    }

    [ComVisible(true)]
    private sealed class VirtualFileDataObject : IDataObject
    {
        private readonly IReadOnlyList<(string Name, byte[] Contents)> files;
        private readonly byte[] descriptor;
        private readonly short descriptorFormat = (short)RegisterClipboardFormatW("FileGroupDescriptorW");
        private readonly short contentsFormat = (short)RegisterClipboardFormatW("FileContents");
        private readonly short dropEffectFormat = (short)RegisterClipboardFormatW("Preferred DropEffect");

        public VirtualFileDataObject(IReadOnlyList<(string Name, byte[] Contents)> files)
        {
            this.files = files;
            descriptor = CreateDescriptor(files);
        }

        private FORMATETC[] Formats =>
        [
            Format(descriptorFormat, TYMED.TYMED_HGLOBAL, -1),
            Format(contentsFormat, TYMED.TYMED_ISTREAM, -1),
            Format(dropEffectFormat, TYMED.TYMED_HGLOBAL, -1),
        ];

        public void GetData(ref FORMATETC format, out STGMEDIUM medium)
        {
            var error = Check(format);
            if (error != 0)
            {
                throw new COMException("Format not offered", error);
            }

            medium = default;
            if (format.cfFormat == contentsFormat)
            {
                var contents = files[format.lindex].Contents;
                medium.tymed = TYMED.TYMED_ISTREAM;
                medium.unionmember = CreateStream(contents);
                return;
            }

            medium.tymed = TYMED.TYMED_HGLOBAL;
            medium.unionmember = ToGlobal(format.cfFormat == descriptorFormat ? descriptor : BitConverter.GetBytes(1));  // DROPEFFECT_COPY
        }

        public void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium) => throw new COMException("Not implemented", NotImplemented);

        public int QueryGetData(ref FORMATETC format) => Check(format);

        public int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut)
        {
            formatOut = formatIn;
            formatOut.ptd = 0;
            return SameFormat;
        }

        public void SetData(ref FORMATETC formatIn, ref STGMEDIUM medium, bool release) => throw new COMException("Not implemented", NotImplemented);

        public IEnumFORMATETC EnumFormatEtc(DATADIR direction) =>
            direction == DATADIR.DATADIR_GET ? new FormatEnumerator(Formats) : throw new COMException("Not implemented", NotImplemented);

        public int DAdvise(ref FORMATETC pFormatetc, ADVF advf, IAdviseSink adviseSink, out int connection)
        {
            connection = 0;
            return AdviseNotSupported;
        }

        public void DUnadvise(int connection) => throw new COMException("Not supported", AdviseNotSupported);

        public int EnumDAdvise(out IEnumSTATDATA enumAdvise)
        {
            enumAdvise = null!;
            return AdviseNotSupported;
        }

        private int Check(FORMATETC format)
        {
            if (format.cfFormat == contentsFormat)
            {
                if ((format.tymed & TYMED.TYMED_ISTREAM) == 0)
                {
                    return DataFormatInvalid;
                }

                return format.lindex >= 0 && format.lindex < files.Count ? 0 : DataIndexInvalid;
            }

            return (format.cfFormat == descriptorFormat || format.cfFormat == dropEffectFormat) && (format.tymed & TYMED.TYMED_HGLOBAL) != 0
                ? 0
                : DataFormatInvalid;
        }

        private static FORMATETC Format(short format, TYMED tymed, int index) =>
            new() { cfFormat = format, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = index, tymed = tymed };

        private static unsafe nint CreateStream(byte[] contents)
        {
            fixed (byte* pointer = contents)
            {
                var stream = SHCreateMemStream(pointer, (uint)contents.Length);
                return stream != 0 ? stream : throw new OutOfMemoryException("SHCreateMemStream failed");
            }
        }
    }

    [ComVisible(true)]
    private sealed class FormatEnumerator(FORMATETC[] formats) : IEnumFORMATETC
    {
        private int position;

        public int Next(int celt, FORMATETC[] rgelt, int[]? pceltFetched)
        {
            var fetched = 0;
            while (fetched < celt && position < formats.Length)
            {
                rgelt[fetched++] = formats[position++];
            }

            if (pceltFetched is { Length: > 0 })
            {
                pceltFetched[0] = fetched;
            }

            return fetched == celt ? 0 : 1;
        }

        public int Skip(int celt)
        {
            position = Math.Min(position + celt, formats.Length);
            return position < formats.Length ? 0 : 1;
        }

        public int Reset()
        {
            position = 0;
            return 0;
        }

        public void Clone(out IEnumFORMATETC newEnum) => newEnum = new FormatEnumerator(formats) { position = position };
    }

#pragma warning disable SYSLIB1054 // LibraryImport cannot marshal a managed COM object; this needs the built-in COM interop.
    [DllImport("ole32.dll")]
    private static extern int OleSetClipboard([MarshalAs(UnmanagedType.Interface)] IDataObject dataObject);
#pragma warning restore SYSLIB1054

    [LibraryImport("shlwapi.dll")]
    private static unsafe partial nint SHCreateMemStream(byte* initial, uint size);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);
}
