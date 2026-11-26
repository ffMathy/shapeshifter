// ail-derived-from: FilePath@5b480e9199c2be8f
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the FilePath node: resolving copied paths and the Copy file at this path action.</summary>
public sealed class FilePathTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
    ];

    [Fact(DisplayName = "example 1: Resolve(the path of an existing file, such as \"/home/anna/report.pdf\") => \"/home/anna/report.pdf\"")]
    public void Example1ResolvesAnExistingFile()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFile("report.pdf");

        Assert.Equal(Path.GetFullPath(path), FilePath.Resolve(path));
    }

    [Fact(DisplayName = "example 2: Resolve(\"\\\"/home/anna/My report.pdf\\\"\") => \"/home/anna/My report.pdf\", when it exists")]
    public void Example2ResolvesAQuotedPath()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFile("My report.pdf");

        Assert.Equal(Path.GetFullPath(path), FilePath.Resolve($"\"{path}\""));
    }

    [Fact(DisplayName = "example 3: Resolve(\"file:///home/anna/My%20report.pdf\") => \"/home/anna/My report.pdf\", when it exists")]
    public void Example3ResolvesAFileLink()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFile("My report.pdf");
        var link = new Uri(path).AbsoluteUri;

        Assert.Contains("%20", link, StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath(path), FilePath.Resolve(link));
    }

    [Fact(DisplayName = "example 4: Resolve(\"~/report.pdf\") => the home folder's report.pdf, when it exists")]
    public void Example4ResolvesAPathInTheHomeFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var name = $"ail-filepath-check-{Guid.NewGuid():N}-report.pdf";
        var path = Path.Combine(home, name);
        File.WriteAllBytes(path, []);
        try
        {
            Assert.Equal(Path.GetFullPath(path), FilePath.Resolve($"~/{name}"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact(DisplayName = "example 5: Resolve(\"~\") => the home folder")]
    public void Example5ResolvesTheHomeFolder()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        Assert.Equal(Path.GetFullPath(home), FilePath.Resolve("~"));
    }

    [Fact(DisplayName = "example 6: Resolve(the path of an existing folder) => that path")]
    public void Example6ResolvesAnExistingFolder()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "documents");
        Directory.CreateDirectory(path);

        Assert.Equal(Path.GetFullPath(path), FilePath.Resolve(path));
    }

    [Fact(DisplayName = "example 7: Resolve(the path of nothing that exists) => nothing")]
    public void Example7GivesNothingForAPathThatDoesNotExist()
    {
        using var folder = new TempFolder();
        var path = Path.Combine(folder.Path, "missing.pdf");

        Assert.Null(FilePath.Resolve(path));
    }

    [Fact(DisplayName = "example 8: Resolve(\"report.pdf\") => nothing; not absolute")]
    public void Example8GivesNothingForARelativePath()
    {
        Assert.Null(FilePath.Resolve("report.pdf"));
    }

    [Fact(DisplayName = "example 9: Resolve(\"\\\\\\\\server\\\\share\\\\a.txt\") => nothing; a network path")]
    public void Example9GivesNothingForANetworkPath()
    {
        Assert.Null(FilePath.Resolve(@"\\server\share\a.txt"));
    }

    [Fact(DisplayName = "example 10: Resolve(\"file://server/share/a.txt\") => nothing")]
    public void Example10GivesNothingForAFileLinkWithAHost()
    {
        Assert.Null(FilePath.Resolve("file://server/share/a.txt"));
    }

    [Fact(DisplayName = "example 11: Resolve(\"/home/anna/a.txt and /home/anna/b.txt\") => nothing, unless a file has that whole name")]
    public void Example11GivesNothingForTwoPathsInOneLine()
    {
        using var folder = new TempFolder();
        var first = folder.CreateFile("a.txt");
        var second = folder.CreateFile("b.txt");

        Assert.Null(FilePath.Resolve($"{first} and {second}"));
    }

    [Fact(DisplayName = "example 12: Resolve(two paths on two lines) => nothing")]
    public void Example12GivesNothingForTwoPathsOnTwoLines()
    {
        using var folder = new TempFolder();
        var first = folder.CreateFile("a.txt");
        var second = folder.CreateFile("b.txt");

        Assert.Null(FilePath.Resolve($"{first}\n{second}"));
    }

    [Fact(DisplayName = "example 13: CanExecute(text: an existing file's path) => true")]
    public void Example13CanExecuteOnAnExistingFilesPath()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFile("report.pdf");
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = path });

        Assert.True(new CopyFileAtPathAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 14: CanExecute(PNG image) => false")]
    public void Example14CannotExecuteOnAnImage()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });

        Assert.False(new CopyFileAtPathAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 15: ExecuteAsync(text \"/home/anna/report.pdf\", which exists) => the clipboard holds { Files: [\"/home/anna/report.pdf\"] }, which is the history's newest item; nothing is pasted")]
    public async Task Example15ExecuteCopiesTheFile()
    {
        using var folder = new TempFolder();
        var path = folder.CreateFile("report.pdf");
        var expected = Path.GetFullPath(path);
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = path });

        await new CopyFileAtPathAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var writes = harness.Backend.Writes;
        Assert.NotEmpty(writes);
        var written = writes[writes.Count - 1];
        Assert.Equal(expected, Assert.Single(written.Files));
        Assert.Null(written.Text);

        var newest = harness.History.Items[0];
        Assert.Equal(expected, Assert.Single(newest.Snapshot.Files));

        Assert.Equal(0, harness.Paste.Calls);
    }

    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ail-filepath-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string CreateFile(string name)
        {
            var path = System.IO.Path.Combine(Path, name);
            File.WriteAllBytes(path, []);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
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
