// ail-derived-from: UploadFile@3b5fb3f50ee68c42
using System.Net;
using System.Net.Http;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the UploadFile node's examples.</summary>
public sealed class UploadFileTests : IDisposable
{
    private readonly string _directory;

    /// <summary>Creates a fresh temporary folder for this test's files.</summary>
    public UploadFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ail-upload-file-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    /// <summary>Removes the temporary folder.</summary>
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
    }

    private string CreateFile(string name, byte[] contents)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, contents);
        return path;
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    [Fact(DisplayName = "example 1: CanExecute(files [an existing file \"report.pdf\" of 3 bytes]) => true")]
    public void Example01ExistingFileOfThreeBytes()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.True(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 2: CanExecute(files [an existing empty file]) => false")]
    public void Example02ExistingEmptyFile()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("empty.txt", []);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 3: CanExecute(files [a folder]) => false")]
    public void Example03Folder()
    {
        using var harness = new ActionTestHarness();
        var folder = Path.Combine(_directory, "folder");
        Directory.CreateDirectory(folder);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [folder] });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 4: CanExecute(files [a file, another file]) => false; one file only")]
    public void Example04TwoFiles()
    {
        using var harness = new ActionTestHarness();
        var first = CreateFile("a.txt", [1, 2, 3]);
        var second = CreateFile("b.txt", [4, 5, 6]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 5: CanExecute(files [the path of nothing]) => false")]
    public void Example05PathOfNothing()
    {
        using var harness = new ActionTestHarness();
        var path = Path.Combine(_directory, "missing.txt");
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 6: CanExecute(files [\"\\\\\\\\server\\\\share\\\\a.txt\"]) => false; a network path")]
    public void Example06NetworkPath()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = ["\\\\server\\share\\a.txt"] });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 7: CanExecute(text \"report.pdf\") => false")]
    public void Example07Text()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "report.pdf" });

        Assert.False(new UploadFileAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 8: GetTitle(files [an existing file]) => \"Upload and paste link\"")]
    public void Example08Title()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        Assert.Equal(Strings.ActionUploadText, new UploadFileAction().GetTitle(item));
    }

    [Fact(DisplayName = "example 9: ExecuteAsync(files [\"…/report.pdf\" holding 01 02 03]) => uploads \"report.pdf\" as application/octet-stream holding 01 02 03")]
    public async Task Example09UploadsFileWithItsNameAndBytes()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });

        string? sentName = null;
        string? sentMediaType = null;
        byte[]? sentBytes = null;
        harness.Handler.Respond = request =>
        {
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var disposition = part.Headers.ContentDisposition;
                    if (disposition?.Name?.Trim('"') != "fileToUpload")
                    {
                        continue;
                    }

                    sentName = (disposition.FileName ?? disposition.FileNameStar)?.Trim('"');
                    sentMediaType = part.Headers.ContentType?.ToString();
                    using var stream = part.ReadAsStream();
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    sentBytes = buffer.ToArray();
                }
            }

            return Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.pdf");
        };

        await new UploadFileAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Equal("report.pdf", sentName);
        Assert.Equal("application/octet-stream", sentMediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, sentBytes);
    }

    [Fact(DisplayName = "example 10: Catbox answers 200 \"https://files.catbox.moe/abc123.pdf\" => pastes the text \"https://files.catbox.moe/abc123.pdf\", and adds it to the history as its newest item")]
    public async Task Example10PastesLinkAndAddsItToHistory()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        harness.Handler.Respond = _ => Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.pdf");

        await new UploadFileAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("https://files.catbox.moe/abc123.pdf", written.Text);
        Assert.Null(written.Image);
        Assert.Empty(written.Files);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal("https://files.catbox.moe/abc123.pdf", harness.History.Items[0].Snapshot.Text);
    }

    [Fact(DisplayName = "example 11: ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again")]
    public async Task Example11SameItemAgainIsNotUploadedTwice()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        harness.Handler.Respond = _ => Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.pdf");
        var action = new UploadFileAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);
        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Handler.RequestedUris);
        Assert.Equal(2, harness.Backend.Writes.Count);
        Assert.Equal("https://files.catbox.moe/abc123.pdf", harness.Backend.Writes[1].Text);
        Assert.Equal(2, harness.Paste.Calls);
    }

    [Fact(DisplayName = "example 12: Catbox answers 412 \"Invalid uploader\" => HttpRequestException; nothing pasted, nothing added to the history")]
    public async Task Example12RefusedUploadPastesNothing()
    {
        using var harness = new ActionTestHarness();
        var path = CreateFile("report.pdf", [1, 2, 3]);
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [path] });
        var historyCount = harness.History.Items.Count;
        harness.Handler.Respond = _ => Answer(HttpStatusCode.PreconditionFailed, "Invalid uploader");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => new UploadFileAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct));

        Assert.Empty(harness.Backend.Writes);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }
}
