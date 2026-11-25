using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Platform;

namespace Shapeshifter.Platform.Windows;

/// <summary>
/// Reads virtual files (<c>FileGroupDescriptorW</c> with one <c>FileContents</c> stream per file) through the OLE
/// clipboard. Remote Desktop offers files copied in a session this way (issue #171), as do Outlook and some archivers.
/// </summary>
/// <remarks>
/// The portable clipboard API cannot ask for one file's contents by index, so this talks to the clipboard's
/// <c>IDataObject</c> directly, through its vtable: built-in COM interop is off in this app. The work runs on a
/// thread of its own in a single-threaded apartment, so a slow transfer from the remote computer never stalls the UI.
/// </remarks>
[SupportedOSPlatform("windows")]
internal sealed unsafe partial class WindowsVirtualFileReader : IVirtualFileReader
{
    private const uint TymedHGlobal = 1;
    private const uint TymedIStream = 4;
    private const uint AspectContent = 1;

    private readonly ILogger logger;

    public WindowsVirtualFileReader(ILogger logger)
    {
        this.logger = logger.ForContext<WindowsVirtualFileReader>();
    }

    public Task<IReadOnlyList<string>> ReadAsync(string parentFolder, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(Read(parentFolder, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                logger.Warning(exception, "Could not read the copied files from the clipboard");
                completion.TrySetResult([]);
            }
        })
        {
            IsBackground = true,
            Name = "Shapeshifter virtual files",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    private IReadOnlyList<string> Read(string parentFolder, CancellationToken cancellationToken)
    {
        var initialized = OleInitialize(0);
        try
        {
            if (OleGetClipboard(out var dataObject) < 0 || dataObject == 0)
            {
                return [];
            }

            try
            {
                var descriptorFormat = (ushort)RegisterClipboardFormatW(FileGroupDescriptor.FormatName);
                var contentsFormat = (ushort)RegisterClipboardFormatW(FileGroupDescriptor.ContentsFormatName);
                var descriptor = GetDescriptor(dataObject, descriptorFormat);
                if (descriptor is not { Length: > 0 })
                {
                    return [];
                }

                var entries = FileGroupDescriptor.Parse(descriptor);
                var paths = VirtualFileStore.Save(
                    parentFolder,
                    descriptor,
                    index => GetContents(dataObject, contentsFormat, index, entries[index].Size),
                    cancellationToken);
                logger.Information("Saved {Count} of {Total} copied virtual files", paths.Count, entries.Count);
                return paths;
            }
            finally
            {
                Release(dataObject);
            }
        }
        finally
        {
            if (initialized >= 0)
            {
                OleUninitialize();
            }
        }
    }

    /// <summary>The contents of one virtual file, as the stream it is offered as or a copy of its memory.</summary>
    private Stream? GetContents(nint dataObject, ushort format, int index, long? size)
    {
        var request = new FormatEtc { Format = format, Aspect = AspectContent, Index = index, Tymed = TymedIStream | TymedHGlobal };
        var medium = default(StgMedium);
        var result = ((delegate* unmanaged[Stdcall]<nint, FormatEtc*, StgMedium*, int>)VTable(dataObject)[3])(dataObject, &request, &medium);
        if (result < 0)
        {
            logger.Debug("Reading virtual file {Index} failed with 0x{Result:X8}", index, result);
            return null;
        }

        if (medium.Tymed == TymedIStream && medium.Handle != 0)
        {
            return new ComStream(medium);
        }

        try
        {
            return medium.Tymed == TymedHGlobal ? new MemoryStream(CopyGlobal(medium.Handle, size)) : null;
        }
        finally
        {
            ReleaseStgMedium(&medium);
        }
    }

    private static byte[]? GetDescriptor(nint dataObject, ushort format)
    {
        var request = new FormatEtc { Format = format, Aspect = AspectContent, Index = -1, Tymed = TymedHGlobal };
        var medium = default(StgMedium);
        if (((delegate* unmanaged[Stdcall]<nint, FormatEtc*, StgMedium*, int>)VTable(dataObject)[3])(dataObject, &request, &medium) < 0)
        {
            return null;
        }

        try
        {
            return medium.Tymed == TymedHGlobal ? CopyGlobal(medium.Handle, null) : null;
        }
        finally
        {
            ReleaseStgMedium(&medium);
        }
    }

    // GlobalSize can round up, so a stated size wins when it is smaller.
    private static byte[] CopyGlobal(nint handle, long? size)
    {
        var length = (long)(ulong)GlobalSize(handle);
        if (size is { } stated && stated >= 0 && stated < length)
        {
            length = stated;
        }

        var pointer = GlobalLock(handle);
        if (pointer == 0)
        {
            return [];
        }

        try
        {
            return new ReadOnlySpan<byte>((void*)pointer, checked((int)length)).ToArray();
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    private static nint* VTable(nint comObject) => *(nint**)comObject;

    private static void Release(nint comObject) =>
        ((delegate* unmanaged[Stdcall]<nint, uint>)VTable(comObject)[2])(comObject);

    /// <summary>Reads an <c>IStream</c> handed over in an <c>STGMEDIUM</c>, and releases the medium when disposed.</summary>
    private sealed class ComStream(StgMedium medium) : Stream
    {
        private StgMedium medium = medium;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || medium.Handle == 0)
            {
                return 0;
            }

            uint read = 0;
            int result;
            fixed (byte* pointer = buffer)
            {
                // ISequentialStream::Read, the first method after IUnknown's three.
                result = ((delegate* unmanaged[Stdcall]<nint, void*, uint, uint*, int>)VTable(medium.Handle)[3])(medium.Handle, pointer, (uint)buffer.Length, &read);
            }

            if (result < 0)
            {
                throw new IOException($"Reading a copied file failed with 0x{result:X8}.");
            }

            return (int)read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (medium.Handle != 0)
            {
                fixed (StgMedium* pointer = &medium)
                {
                    ReleaseStgMedium(pointer);
                }

                medium = default;
            }

            base.Dispose(disposing);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormatEtc
    {
        public ushort Format;
        public nint TargetDevice;
        public uint Aspect;
        public int Index;
        public uint Tymed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StgMedium
    {
        public uint Tymed;
        public nint Handle;
        public nint UnknownForRelease;
    }

    [LibraryImport("ole32.dll")]
    private static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    private static partial void OleUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial int OleGetClipboard(out nint dataObject);

    [LibraryImport("ole32.dll")]
    private static partial void ReleaseStgMedium(StgMedium* medium);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint handle);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint handle);
}
