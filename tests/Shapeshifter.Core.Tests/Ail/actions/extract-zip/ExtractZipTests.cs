// ail-derived-from: ExtractZip@02004e3f03edf4b2
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the ExtractZip node's examples.</summary>
public sealed class ExtractZipTests
{
    private static string InputFolder(ActionTestHarness harness)
    {
        var folder = Path.Combine(harness.Paths.TempDirectory, "input");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string WriteZip(string folder, string fileName, params (string Name, string Content)[] entries)
    {
        var path = Path.Combine(folder, fileName);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
        }

        return path;
    }

    private static void AssertExtractedPath(string path, string temp, string folderName, string relative)
    {
        Assert.True(Path.IsPathFullyQualified(path), $"Not a full path: {path}");
        var root = Path.GetFullPath(Path.Combine(temp, "Extracted files"));
        var rel = Path.GetRelativePath(root, Path.GetFullPath(path)).Replace('\\', '/');
        Assert.Matches(
            "^[0-9a-fA-F]{8}/" + Regex.Escape(folderName) + "/" + Regex.Escape(relative) + "$",
            rel);
    }

    [Fact(DisplayName = "example 1: IsZip(an existing file \"photos.zip\" made by a zip tool) => true")]
    public void Example01()
    {
        using var harness = new ActionTestHarness();
        var path = WriteZip(InputFolder(harness), "photos.zip", ("a.jpg", "image"));

        Assert.True(ExtractZipAction.IsZip(path));
    }

    [Fact(DisplayName = "example 2: IsZip(an existing file \"PHOTOS.ZIP\") => true")]
    public void Example02()
    {
        using var harness = new ActionTestHarness();
        var path = WriteZip(InputFolder(harness), "PHOTOS.ZIP", ("a.jpg", "image"));

        Assert.True(ExtractZipAction.IsZip(path));
    }

    [Fact(DisplayName = "example 3: IsZip(an existing file \"photos.zip\" holding \"hello\") => false; not an archive")]
    public void Example03()
    {
        using var harness = new ActionTestHarness();
        var path = Path.Combine(InputFolder(harness), "photos.zip");
        File.WriteAllText(path, "hello");

        Assert.False(ExtractZipAction.IsZip(path));
    }

    [Fact(DisplayName = "example 4: IsZip(an existing archive named \"photos.bin\") => false; the name must end in .zip")]
    public void Example04()
    {
        using var harness = new ActionTestHarness();
        var path = WriteZip(InputFolder(harness), "photos.bin", ("a.jpg", "image"));

        Assert.False(ExtractZipAction.IsZip(path));
    }

    [Fact(DisplayName = "example 5: IsZip(the path of nothing) => false")]
    public void Example05()
    {
        using var harness = new ActionTestHarness();
        var path = Path.Combine(InputFolder(harness), "missing.zip");

        Assert.False(ExtractZipAction.IsZip(path));
    }

    [Fact(DisplayName = "example 6: Extract(an archive \"trip.zip\" holding \"a.jpg\", \"b.jpg\" and \"notes/day1.txt\", temp \"/tmp/s\") => [\"/tmp/s/Extracted files/<8 hex>/trip/a.jpg\", \"/tmp/s/Extracted files/<8 hex>/trip/b.jpg\", \"/tmp/s/Extracted files/<8 hex>/trip/notes\"]; notes/day1.txt is inside the notes folder")]
    public void Example06()
    {
        using var harness = new ActionTestHarness();
        var zip = WriteZip(
            InputFolder(harness),
            "trip.zip",
            ("a.jpg", "a"),
            ("b.jpg", "b"),
            ("notes/day1.txt", "day one"));
        var temp = harness.Paths.TempDirectory;

        var result = ExtractZipAction.Extract(zip, temp);

        Assert.Equal(3, result.Count);
        AssertExtractedPath(result[0], temp, "trip", "a.jpg");
        AssertExtractedPath(result[1], temp, "trip", "b.jpg");
        AssertExtractedPath(result[2], temp, "trip", "notes");
        Assert.True(File.Exists(result[0]));
        Assert.True(File.Exists(result[1]));
        Assert.True(Directory.Exists(result[2]));
        Assert.True(File.Exists(Path.Combine(result[2], "day1.txt")));
    }

    [Fact(DisplayName = "example 7: Extract(an archive holding an entry \"../evil.txt\") => InvalidDataException; nothing is written")]
    public void Example07()
    {
        using var harness = new ActionTestHarness();
        var zip = WriteZip(InputFolder(harness), "trip.zip", ("../evil.txt", "evil"));
        var temp = harness.Paths.TempDirectory;

        Assert.Throws<InvalidDataException>(() => ExtractZipAction.Extract(zip, temp));

        var extracted = Path.Combine(temp, "Extracted files");
        if (Directory.Exists(extracted))
        {
            Assert.Empty(Directory.GetFiles(extracted, "*", SearchOption.AllDirectories));
        }

        Assert.Empty(Directory.GetFiles(temp, "evil.txt", SearchOption.AllDirectories));
    }

    [Fact(DisplayName = "example 8: CanExecute(files [an archive \"trip.zip\"]) => true")]
    public void Example08()
    {
        using var harness = new ActionTestHarness();
        var zip = WriteZip(InputFolder(harness), "trip.zip", ("a.jpg", "a"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [zip] });

        Assert.True(new ExtractZipAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 9: CanExecute(files [an archive, another archive]) => false; only one file")]
    public void Example09()
    {
        using var harness = new ActionTestHarness();
        var folder = InputFolder(harness);
        var first = WriteZip(folder, "trip.zip", ("a.jpg", "a"));
        var second = WriteZip(folder, "other.zip", ("b.jpg", "b"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });

        Assert.False(new ExtractZipAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 10: CanExecute(text \"trip.zip\") => false")]
    public void Example10()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "trip.zip" });

        Assert.False(new ExtractZipAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 11: ExecuteAsync(files [\"trip.zip\" holding \"a.jpg\"]) => pastes { Files: [\"<temp>/Extracted files/<8 hex>/trip/a.jpg\"] }; the history is unchanged")]
    public async Task Example11()
    {
        using var harness = new ActionTestHarness();
        var zip = WriteZip(InputFolder(harness), "trip.zip", ("a.jpg", "a"));
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [zip] });
        var historyCount = harness.History.Items.Count;
        var historyNewest = harness.History.Items[0];

        await new ExtractZipAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        var file = Assert.Single(written.Files);
        AssertExtractedPath(file, harness.Paths.TempDirectory, "trip", "a.jpg");
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
        Assert.Same(historyNewest, harness.History.Items[0]);
    }
}
