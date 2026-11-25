using System.Text;
using Shapeshifter.Core.Clipboard;

namespace Shapeshifter.Core.Tests.Clipboard;

public sealed class VirtualFileStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "shapeshifter-virtual-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static Func<int, Stream?> Contents(params string?[] files) =>
        index => files[index] is { } text ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null;

    [Fact]
    public void Parse_ReadsWhatCreateWrote()
    {
        VirtualFileDescriptor[] entries =
        [
            new("report.txt", false, 5),
            new("Photos", true, null),
            new(@"Photos\beach.jpg", false, 5_000_000_000),
        ];

        var data = FileGroupDescriptor.Create(entries);

        Assert.Equal(4 + (3 * FileGroupDescriptor.DescriptorSize), data.Length);
        Assert.Equal(entries, FileGroupDescriptor.Parse(data));
    }

    [Fact]
    public void Parse_StopsAtTheEndOfTheData()
    {
        var data = FileGroupDescriptor.Create([new("a.txt", false, 1), new("b.txt", false, 1)]);

        Assert.Equal(["a.txt"], FileGroupDescriptor.Parse(data.AsSpan(0, data.Length - 1)).Select(entry => entry.Name));
        Assert.Empty(FileGroupDescriptor.Parse([1, 0]));
    }

    [Fact]
    public void Save_WritesFilesAndFoldersAndReturnsTheTopLevelOnes()
    {
        var descriptor = FileGroupDescriptor.Create(
        [
            new("report.txt", false, 6),
            new("Photos", true, null),
            new(@"Photos\Summer", true, null),
            new(@"Photos\Summer\beach.txt", false, 4),
        ]);

        var paths = VirtualFileStore.Save(root, descriptor, Contents("report", null, null, "sand"), TestContext.Current.CancellationToken);

        Assert.Equal(2, paths.Count);
        Assert.Equal("report.txt", Path.GetFileName(paths[0]));
        Assert.Equal("Photos", Path.GetFileName(paths[1]));
        Assert.Equal("report", File.ReadAllText(paths[0]));
        Assert.Equal("sand", File.ReadAllText(Path.Combine(paths[1], "Summer", "beach.txt")));
    }

    [Fact]
    public void Save_TheSameCopyAgain_ReturnsTheSamePathsWithoutReadingAgain()
    {
        var descriptor = FileGroupDescriptor.Create([new("a.txt", false, 1)]);
        var first = VirtualFileStore.Save(root, descriptor, Contents("1"), TestContext.Current.CancellationToken);

        var second = VirtualFileStore.Save(root, descriptor, _ => throw new InvalidOperationException("read again"), TestContext.Current.CancellationToken);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Save_DifferentFiles_GetDifferentFolders()
    {
        var first = VirtualFileStore.Save(root, FileGroupDescriptor.Create([new("a.txt", false, 1)]), Contents("1"), TestContext.Current.CancellationToken);
        var second = VirtualFileStore.Save(root, FileGroupDescriptor.Create([new("a.txt", false, 2)]), Contents("22"), TestContext.Current.CancellationToken);

        Assert.NotEqual(first, second);
        Assert.Equal("22", File.ReadAllText(second[0]));
    }

    [Fact]
    public void Save_AFileThatCannotBeRead_KeepsNothing()
    {
        var descriptor = FileGroupDescriptor.Create([new("a.txt", false, 1), new("b.txt", false, 1)]);

        Assert.Throws<IOException>(() => VirtualFileStore.Save(root, descriptor, Contents("1", null), TestContext.Current.CancellationToken));

        Assert.Empty(Directory.GetFileSystemEntries(root));
    }

    [Fact]
    public void Save_MoreThanTheLimit_KeepsNothing()
    {
        var descriptor = FileGroupDescriptor.Create([new("huge.bin", false, VirtualFileStore.MaxTotalBytes + 1)]);

        Assert.Empty(VirtualFileStore.Save(root, descriptor, _ => throw new InvalidOperationException("read"), TestContext.Current.CancellationToken));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void Save_NamesCannotLeaveTheFolder()
    {
        var descriptor = FileGroupDescriptor.Create([new(@"..\..\evil.txt", false, 1), new(@"C:\Windows\x.txt", false, 1)]);

        var paths = VirtualFileStore.Save(root, descriptor, Contents("e", "x"), TestContext.Current.CancellationToken);

        Assert.All(paths, path => Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, Path.GetFullPath(path), StringComparison.Ordinal));
        Assert.Equal(["evil.txt", "C_"], paths.Select(Path.GetFileName));
    }

    [Theory]
    [InlineData("a.txt", "a.txt")]
    [InlineData(@"dir\a.txt", "dir/a.txt")]
    [InlineData(@"..\a.txt", "a.txt")]
    [InlineData(@"\\server\share\a.txt", "server/share/a.txt")]
    [InlineData(@"a\...\b", "a/b")]
    [InlineData("what?.txt", "what_.txt")]
    [InlineData("..", null)]
    [InlineData("", null)]
    public void ToRelativePath_KeepsOnlySafeSegments(string name, string? expected)
    {
        Assert.Equal(expected?.Replace('/', Path.DirectorySeparatorChar), VirtualFileStore.ToRelativePath(name));
    }

    [Theory]
    [InlineData("FileGroupDescriptorW")]
    [InlineData("FileGroupDescriptor")]
    [InlineData("FileContents")]
    public void VirtualFileFormats_AreNotKeptAsRawBytes(string name)
    {
        Assert.False(ClipboardFormatClassifier.ShouldCapture(name));
    }
}
