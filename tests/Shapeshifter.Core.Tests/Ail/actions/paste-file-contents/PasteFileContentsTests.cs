// ail-derived-from: PasteFileContents@27f14f02df2db0e5
using System.Text;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PasteFileContents node's examples.</summary>
public sealed class PasteFileContentsTests : IDisposable
{
    private readonly string _directory;

    /// <summary>Creates a fresh temporary directory for the files of one test.</summary>
    public PasteFileContentsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PasteFileContentsTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the temporary directory of this test.</summary>
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

    private string WriteFile(string name, byte[] bytes)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Fact(DisplayName = "example 1: ReadText(a file holding the UTF-8 bytes of \"line one\\nline two\\n\") => \"line one\\nline two\\n\"")]
    public void Example1()
    {
        var path = WriteFile("lines.txt", Encoding.UTF8.GetBytes("line one\nline two\n"));

        Assert.Equal("line one\nline two\n", PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 2: ReadText(a file holding EF BB BF and then \"héllo\") => \"héllo\"")]
    public void Example2()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("héllo")];
        var path = WriteFile("bom.txt", bytes);

        Assert.Equal("héllo", PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 3: ReadText(a file holding FF FE and then \"hi\" in UTF-16 little endian) => \"hi\"")]
    public void Example3()
    {
        var path = WriteFile("utf16.txt", [0xFF, 0xFE, 0x68, 0x00, 0x69, 0x00]);

        Assert.Equal("hi", PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 4: ReadText(a file holding the bytes 89 50 4E 47 00 01, a PNG's start) => nothing")]
    public void Example4()
    {
        var path = WriteFile("image.png", [0x89, 0x50, 0x4E, 0x47, 0x00, 0x01]);

        Assert.Null(PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 5: ReadText(a file holding C3 28) => nothing; not valid UTF-8")]
    public void Example5()
    {
        var path = WriteFile("invalid.txt", [0xC3, 0x28]);

        Assert.Null(PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 6: ReadText(an empty file) => nothing")]
    public void Example6()
    {
        var path = WriteFile("empty.txt", []);

        Assert.Null(PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 7: ReadText(a file of 1048577 bytes of \"a\") => nothing; larger than MaxBytes")]
    public void Example7()
    {
        var bytes = new byte[1048577];
        Array.Fill(bytes, (byte)'a');
        var path = WriteFile("large.txt", bytes);

        Assert.Null(PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 8: ReadText(a folder's path) => nothing")]
    public void Example8()
    {
        var folder = Path.Combine(_directory, "folder");
        Directory.CreateDirectory(folder);

        Assert.Null(PasteFileContentsAction.ReadText(folder));
    }

    [Fact(DisplayName = "example 9: ReadText(the path of nothing) => nothing")]
    public void Example9()
    {
        var path = Path.Combine(_directory, "missing.txt");

        Assert.Null(PasteFileContentsAction.ReadText(path));
    }

    [Fact(DisplayName = "example 10: CanExecute(files [a text file]) => true")]
    public void Example10()
    {
        using var harness = new ActionTestHarness();
        var path = WriteFile("notes.txt", Encoding.UTF8.GetBytes("some notes"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.True(new PasteFileContentsAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 11: CanExecute(files [a text file, another text file]) => false; only one file")]
    public void Example11()
    {
        using var harness = new ActionTestHarness();
        var first = WriteFile("first.txt", Encoding.UTF8.GetBytes("first"));
        var second = WriteFile("second.txt", Encoding.UTF8.GetBytes("second"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });

        Assert.False(new PasteFileContentsAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 12: CanExecute(text \"hello\") => false")]
    public void Example12()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        Assert.False(new PasteFileContentsAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 13: ExecuteAsync(files [a file holding \"a\\r\\nb\"]) => pastes { Text: \"a\\r\\nb\" }; the history is unchanged")]
    public async Task Example13()
    {
        using var harness = new ActionTestHarness();
        var path = WriteFile("crlf.txt", Encoding.UTF8.GetBytes("a\r\nb"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        var historyBefore = harness.History.Items.Count;

        await new PasteFileContentsAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Backend.Writes);
        Assert.Equal("a\r\nb", harness.Backend.Writes[0].Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
