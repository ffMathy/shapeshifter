// ail-derived-from: UploadText@9ce49822e8a8bf33
using System.Net;
using System.Net.Http;
using System.Text;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the UploadText node: uploading a text to Catbox and pasting its link.</summary>
public sealed class UploadTextTests
{
    private sealed record Upload(string? FileName, string? MediaType, string? CharSet, byte[] Data);

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body) };

    private static byte[] ReadAll(HttpContent content)
    {
        using var stream = content.ReadAsStream();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static Upload? ReadUpload(HttpRequestMessage request)
    {
        if (request.Content is not MultipartFormDataContent multipart)
        {
            return null;
        }

        foreach (var part in multipart)
        {
            var disposition = part.Headers.ContentDisposition;
            if (disposition?.Name?.Trim('"') == "fileToUpload")
            {
                return new Upload(
                    disposition.FileName?.Trim('"') ?? disposition.FileNameStar,
                    part.Headers.ContentType?.MediaType,
                    part.Headers.ContentType?.CharSet,
                    ReadAll(part));
            }
        }

        return null;
    }

    [Fact(DisplayName = "example 1: ExecuteAsync(text \"héllo wörld\" captured 2026-11-20 13:05:09 local) => uploads \"Text 2026-11-20 13.05.09.txt\" as text/plain; charset=utf-8, holding EF BB BF then \"héllo wörld\" in UTF-8")]
    public async Task Example1UploadsTextFileWithByteOrderMark()
    {
        using var harness = new ActionTestHarness();
        var uploads = new List<Upload?>();
        harness.Handler.Respond = request =>
        {
            uploads.Add(ReadUpload(request));
            return Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.txt");
        };
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "héllo wörld" });
        item.CapturedAt = new DateTimeOffset(new DateTime(2026, 11, 20, 13, 5, 9, DateTimeKind.Local));
        var action = new UploadTextAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var upload = Assert.Single(uploads);
        Assert.NotNull(upload);
        Assert.Equal("Text 2026-11-20 13.05.09.txt", upload.FileName);
        Assert.Equal("text/plain", upload.MediaType);
        Assert.Equal("utf-8", upload.CharSet, ignoreCase: true);
        byte[] expected = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("héllo wörld")];
        Assert.Equal(expected, upload.Data);
    }

    [Fact(DisplayName = "example 2: Catbox answers 200 \"https://files.catbox.moe/abc123.txt\" => pastes the text \"https://files.catbox.moe/abc123.txt\", and adds it to the history as its newest item")]
    public async Task Example2PastesLinkAndAddsItToHistory()
    {
        using var harness = new ActionTestHarness();
        harness.Handler.Respond = _ => Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.txt");
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "some log lines" });
        var action = new UploadTextAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("https://files.catbox.moe/abc123.txt", written.Text);
        Assert.Null(written.Image);
        Assert.Empty(written.Files);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal("https://files.catbox.moe/abc123.txt", harness.History.Items[0].Snapshot.Text);
    }

    [Fact(DisplayName = "example 3: ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again")]
    public async Task Example3SameItemAgainIsNotUploadedTwice()
    {
        using var harness = new ActionTestHarness();
        harness.Handler.Respond = _ => Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.txt");
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "a snippet of code" });
        var action = new UploadTextAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);
        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Handler.RequestedUris);
        Assert.Equal(2, harness.Paste.Calls);
        Assert.Equal(2, harness.Backend.Writes.Count);
        Assert.All(harness.Backend.Writes, w => Assert.Equal("https://files.catbox.moe/abc123.txt", w.Text));
    }

    [Fact(DisplayName = "example 4: Catbox answers 412 \"Invalid uploader\" => HttpRequestException; nothing pasted, nothing added to the history")]
    public async Task Example4RefusedUploadPastesNothing()
    {
        using var harness = new ActionTestHarness();
        harness.Handler.Respond = _ => Answer(HttpStatusCode.PreconditionFailed, "Invalid uploader");
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "a long message" });
        var historyBefore = harness.History.Items.Count;
        var action = new UploadTextAction();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct));

        Assert.Empty(harness.Backend.Writes);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Count);
    }

    [Fact(DisplayName = "example 5: CanExecute(text \"   \") => false")]
    public void Example5WhitespaceTextCannotBeUploaded()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "   " });
        var action = new UploadTextAction();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 6: CanExecute(raw \"text/html\" only, no plain text) => false")]
    public void Example6NoPlainTextCannotBeUploaded()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot());
        var action = new UploadTextAction();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 7: CanExecute(text \"caption\" + image, as office suites copy) => true; the text is uploaded, not the image")]
    public async Task Example7TextWithImageUploadsTheText()
    {
        using var harness = new ActionTestHarness();
        var uploads = new List<Upload?>();
        harness.Handler.Respond = request =>
        {
            uploads.Add(ReadUpload(request));
            return Answer(HttpStatusCode.OK, "https://files.catbox.moe/abc123.txt");
        };
        byte[] image = [0x89, 0x50, 0x4E, 0x47];
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "caption", Image = image });
        var action = new UploadTextAction();

        Assert.True(action.CanExecute(item));

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var upload = Assert.Single(uploads);
        Assert.NotNull(upload);
        Assert.Equal("text/plain", upload.MediaType);
        byte[] expected = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("caption")];
        Assert.Equal(expected, upload.Data);
    }
}
