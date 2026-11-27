// ail-derived-from: PasteFileAsImage@29f3ca3a08e878e7
using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteFileAsImage node's examples.</summary>
public sealed class PasteFileAsImageTests : IDisposable
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private readonly string _directory;

    /// <summary>Creates a fresh temporary folder for this test.</summary>
    public PasteFileAsImageTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "shapeshifter-paste-file-as-image-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes this test's temporary folder.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private static byte[] PngBytes() =>
        [.. PngSignature, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01];

    private static byte[] JpegBytes() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>A file holding a PNG's bytes gives those bytes.</summary>
    [Fact(DisplayName = "example 1: ReadImage(a file holding a PNG's bytes) => those bytes")]
    public void Example1()
    {
        var bytes = PngBytes();
        var path = WriteFile("picture.png", bytes);

        var result = PasteFileAsImageAction.ReadImage(path);

        Assert.NotNull(result);
        Assert.Equal(bytes, result);
    }

    /// <summary>A file named .txt holding a JPEG's bytes gives those bytes.</summary>
    [Fact(DisplayName = "example 2: ReadImage(a file named photo.txt holding a JPEG's bytes) => those bytes; the start counts, not the name")]
    public void Example2()
    {
        var bytes = JpegBytes();
        var path = WriteFile("photo.txt", bytes);

        var result = PasteFileAsImageAction.ReadImage(path);

        Assert.NotNull(result);
        Assert.Equal(bytes, result);
    }

    /// <summary>A file named .png holding text gives nothing.</summary>
    [Fact(DisplayName = "example 3: ReadImage(a file named image.png holding \"hello\") => nothing")]
    public void Example3()
    {
        var path = WriteFile("image.png", "hello"u8.ToArray());

        var result = PasteFileAsImageAction.ReadImage(path);

        Assert.Null(result);
    }

    /// <summary>A file holding only the first 4 bytes of a PNG's start gives nothing.</summary>
    [Fact(DisplayName = "example 4: ReadImage(a file holding the 4 bytes 89 50 4E 47) => nothing; too short for a PNG's start")]
    public void Example4()
    {
        var path = WriteFile("short.png", [0x89, 0x50, 0x4E, 0x47]);

        var result = PasteFileAsImageAction.ReadImage(path);

        Assert.Null(result);
    }

    /// <summary>A folder's path gives nothing.</summary>
    [Fact(DisplayName = "example 5: ReadImage(a folder's path) => nothing")]
    public void Example5()
    {
        var result = PasteFileAsImageAction.ReadImage(_directory);

        Assert.Null(result);
    }

    /// <summary>A file larger than MaxBytes gives nothing.</summary>
    [Fact(DisplayName = "example 6: ReadImage(a file of 26214401 bytes starting as a PNG) => nothing; larger than MaxBytes")]
    public void Example6()
    {
        var bytes = new byte[26214401];
        PngSignature.CopyTo(bytes, 0);
        var path = WriteFile("huge.png", bytes);

        var result = PasteFileAsImageAction.ReadImage(path);

        Assert.Null(result);
    }

    /// <summary>One PNG file can be pasted as an image.</summary>
    [Fact(DisplayName = "example 7: CanExecute(files [a PNG file]) => true")]
    public void Example7()
    {
        using var harness = new ActionTestHarness();
        var path = WriteFile("picture.png", PngBytes());
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        var action = new PasteFileAsImageAction();

        Assert.True(action.CanExecute(item));
    }

    /// <summary>Two PNG files cannot be pasted as an image.</summary>
    [Fact(DisplayName = "example 8: CanExecute(files [a PNG file, another PNG file]) => false; only one file")]
    public void Example8()
    {
        using var harness = new ActionTestHarness();
        var first = WriteFile("first.png", PngBytes());
        var second = WriteFile("second.png", PngBytes());
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });
        var action = new PasteFileAsImageAction();

        Assert.False(action.CanExecute(item));
    }

    /// <summary>An item that already is an image is not offered.</summary>
    [Fact(DisplayName = "example 9: CanExecute(PNG image) => false; already an image")]
    public void Example9()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes() });
        var action = new PasteFileAsImageAction();

        Assert.False(action.CanExecute(item));
    }

    /// <summary>Running the action pastes the file's bytes as an image and leaves the history alone.</summary>
    [Fact(DisplayName = "example 10: ExecuteAsync(files [a PNG file]) => pastes { Image: the file's bytes }; the history is unchanged")]
    public async Task Example10()
    {
        using var harness = new ActionTestHarness();
        var bytes = PngBytes();
        var path = WriteFile("picture.png", bytes);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        var historyCountBefore = harness.History.Items.Count;
        var action = new PasteFileAsImageAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var write = Assert.Single(harness.Backend.Writes);
        Assert.Equal(bytes, write.Image);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyCountBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
