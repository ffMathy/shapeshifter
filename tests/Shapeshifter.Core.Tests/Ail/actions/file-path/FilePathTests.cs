// ail-derived-from: FilePath@b9fb1758ede4a091
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the FilePath node: Resolve and the two path actions.</summary>
public sealed class FilePathTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89,
    ];

    [Fact(DisplayName = "example 1: Resolve(the path of an existing file, such as \"/home/anna/report.pdf\") => \"/home/anna/report.pdf\"")]
    public void Example1ExistingFile()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("report.pdf");

        Assert.Equal(Path.GetFullPath(file), FilePath.Resolve(file));
    }

    [Fact(DisplayName = "example 2: Resolve(\"\\\"/home/anna/My report.pdf\\\"\") => \"/home/anna/My report.pdf\", when it exists")]
    public void Example2QuotedPath()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("My report.pdf");

        Assert.Equal(Path.GetFullPath(file), FilePath.Resolve("\"" + file + "\""));
    }

    [Fact(DisplayName = "example 3: Resolve(\"file:///home/anna/My%20report.pdf\") => \"/home/anna/My report.pdf\", when it exists")]
    public void Example3FileLink()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("My report.pdf");
        var link = new Uri(file).AbsoluteUri;

        Assert.Contains("%20", link, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(file), FilePath.Resolve(link));
    }

    [Fact(DisplayName = "example 4: Resolve(\"~/report.pdf\") => the home folder's report.pdf, when it exists")]
    public void Example4HomeRelative()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var name = "shapeshifter-filepath-check-" + Guid.NewGuid().ToString("N") + ".pdf";
        var file = Path.Combine(home, name);
        File.WriteAllText(file, "report");
        try
        {
            Assert.Equal(Path.GetFullPath(file), FilePath.Resolve("~/" + name));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact(DisplayName = "example 5: Resolve(\"~\") => the home folder")]
    public void Example5Home()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.GetFullPath(home), FilePath.Resolve("~"));
    }

    [Fact(DisplayName = "example 6: Resolve(the path of an existing folder) => that path")]
    public void Example6ExistingFolder()
    {
        using var temp = new TempFolder();
        var folder = temp.CreateFolder("docs");

        Assert.Equal(Path.GetFullPath(folder), FilePath.Resolve(folder));
    }

    [Fact(DisplayName = "example 7: Resolve(the path of nothing that exists) => nothing")]
    public void Example7Missing()
    {
        using var temp = new TempFolder();
        var missing = Path.Combine(temp.Root, "missing.pdf");

        Assert.Null(FilePath.Resolve(missing));
    }

    [Fact(DisplayName = "example 8: Resolve(\"report.pdf\") => nothing; not absolute")]
    public void Example8Relative()
    {
        Assert.Null(FilePath.Resolve("report.pdf"));
    }

    [Fact(DisplayName = "example 9: Resolve(\"\\\\\\\\server\\\\share\\\\a.txt\") => nothing; a network path")]
    public void Example9UncPath()
    {
        Assert.Null(FilePath.Resolve("\\\\server\\share\\a.txt"));
    }

    [Fact(DisplayName = "example 10: Resolve(\"file://server/share/a.txt\") => nothing")]
    public void Example10FileLinkWithHost()
    {
        Assert.Null(FilePath.Resolve("file://server/share/a.txt"));
    }

    [Fact(DisplayName = "example 11: Resolve(\"/home/anna/a.txt and /home/anna/b.txt\") => nothing, unless a file has that whole name")]
    public void Example11TwoPathsInOneLine()
    {
        using var temp = new TempFolder();
        var a = temp.CreateFile("a.txt");
        var b = temp.CreateFile("b.txt");

        Assert.Null(FilePath.Resolve(a + " and " + b));
    }

    [Fact(DisplayName = "example 12: Resolve(two paths on two lines) => nothing")]
    public void Example12TwoLines()
    {
        using var temp = new TempFolder();
        var a = temp.CreateFile("a.txt");
        var b = temp.CreateFile("b.txt");

        Assert.Null(FilePath.Resolve(a + "\n" + b));
    }

    [Fact(DisplayName = "example 13: CanExecute of either action(text: an existing file's path) => true")]
    public void Example13CanExecuteExistingPath()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("report.pdf");
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = file });

        IClipboardAction[] actions = [new CopyFileAtPathAction(), new ShowPathInFileManagerAction()];
        foreach (var action in actions)
        {
            Assert.True(action.CanExecute(item));
        }
    }

    [Fact(DisplayName = "example 14: CanExecute of either action(PNG image) => false")]
    public void Example14CanExecuteImage()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });

        IClipboardAction[] actions = [new CopyFileAtPathAction(), new ShowPathInFileManagerAction()];
        foreach (var action in actions)
        {
            Assert.False(action.CanExecute(item));
        }
    }

    [Fact(DisplayName = "example 15: ExecuteAsync of CopyFileAtPathAction(text \"/home/anna/report.pdf\", which exists) => the clipboard holds { Files: [\"/home/anna/report.pdf\"] }, which is the history's newest item; nothing is pasted")]
    public async Task Example15CopyFileAtPath()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("report.pdf");
        var expected = Path.GetFullPath(file);
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = file });

        await new CopyFileAtPathAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.NotEmpty(harness.Backend.Writes);
        var written = harness.Backend.Writes[harness.Backend.Writes.Count - 1];
        Assert.Equal(new[] { expected }, written.Files);
        Assert.Equal(new[] { expected }, harness.History.Items[0].Snapshot.Files);
        Assert.Equal(0, harness.Paste.Calls);
    }

    [Fact(DisplayName = "example 16: ExecuteAsync of ShowPathInFileManagerAction(the same) => Launcher.RevealInFileManager(\"/home/anna/report.pdf\"); the history is unchanged")]
    public async Task Example16ShowPathInFileManager()
    {
        using var temp = new TempFolder();
        var file = temp.CreateFile("report.pdf");
        var expected = Path.GetFullPath(file);
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = file });
        var historyBefore = harness.History.Items.Select(i => i.Id).ToList();
        var writesBefore = harness.Backend.Writes.Count;

        await new ShowPathInFileManagerAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Equal(new[] { expected }, harness.Launcher.RevealedPaths);
        Assert.Equal(historyBefore, harness.History.Items.Select(i => i.Id).ToList());
        Assert.Equal(writesBefore, harness.Backend.Writes.Count);
        Assert.Equal(0, harness.Paste.Calls);
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "shapeshifter-filepath-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreateFile(string name)
        {
            var path = Path.Combine(Root, name);
            File.WriteAllText(path, "content");
            return path;
        }

        public string CreateFolder(string name)
        {
            var path = Path.Combine(Root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
