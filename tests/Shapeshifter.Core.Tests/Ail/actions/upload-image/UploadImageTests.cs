// ail-derived-from: UploadImage@8971e558af1cf0aa
using System.Net;
using System.Net.Http;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Checks derived from the UploadImage spec's examples.</summary>
public sealed class UploadImageTests
{
    private const string Link = "https://files.catbox.moe/abc123.png";

    private static readonly byte[] Png =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00,
    ];

    private static readonly byte[] Jpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
        0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
        0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9,
    ];

    private sealed record Upload(string? FileName, string? MediaType);

    private static string? Unquote(string? value) => value?.Trim('"');

    private static Upload ReadUpload(HttpRequestMessage request)
    {
        if (request.Content is MultipartFormDataContent multipart)
        {
            foreach (HttpContent part in multipart)
            {
                var disposition = part.Headers.ContentDisposition;
                if (disposition is not null && Unquote(disposition.Name) == "fileToUpload")
                {
                    return new Upload(
                        Unquote(disposition.FileName ?? disposition.FileNameStar),
                        part.Headers.ContentType?.MediaType);
                }
            }
        }

        return new Upload(null, null);
    }

    private static List<Upload> Answer(ActionTestHarness harness, HttpStatusCode status, string body)
    {
        var uploads = new List<Upload>();
        harness.Handler.Respond = request =>
        {
            uploads.Add(ReadUpload(request));
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        };
        return uploads;
    }

    [Fact(DisplayName = "example 1: ExecuteAsync(PNG captured 2026-11-20 13:05:09 local) => uploads \"Image 2026-11-20 13.05.09.png\" as image/png")]
    public async Task Example1()
    {
        using var harness = new ActionTestHarness();
        var uploads = Answer(harness, HttpStatusCode.OK, Link);
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png });
        item.CapturedAt = new DateTimeOffset(new DateTime(2026, 11, 20, 13, 5, 9, DateTimeKind.Local));
        var action = new UploadImageAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(uploads);
        Assert.Equal("Image 2026-11-20 13.05.09.png", uploads[0].FileName);
        Assert.Equal("image/png", uploads[0].MediaType);
    }

    [Fact(DisplayName = "example 2: ExecuteAsync(JPEG image) => uploads \"Image <time>.jpg\" as image/jpeg")]
    public async Task Example2()
    {
        using var harness = new ActionTestHarness();
        var uploads = Answer(harness, HttpStatusCode.OK, "https://files.catbox.moe/abc123.jpg");
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Jpeg });
        var action = new UploadImageAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(uploads);
        Assert.Matches(@"^Image \d{4}-\d{2}-\d{2} \d{2}\.\d{2}\.\d{2}\.jpg$", uploads[0].FileName ?? string.Empty);
        Assert.Equal("image/jpeg", uploads[0].MediaType);
    }

    [Fact(DisplayName = "example 3: Catbox answers 200 \"https://files.catbox.moe/abc123.png\" => pastes the text \"https://files.catbox.moe/abc123.png\", and adds it to the history as its newest item")]
    public async Task Example3()
    {
        using var harness = new ActionTestHarness();
        Answer(harness, HttpStatusCode.OK, Link);
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png });
        var action = new UploadImageAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Backend.Writes);
        Assert.Equal(Link, harness.Backend.Writes[0].Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(Link, harness.History.Items[0].Snapshot.Text);
    }

    [Fact(DisplayName = "example 4: ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again")]
    public async Task Example4()
    {
        using var harness = new ActionTestHarness();
        Answer(harness, HttpStatusCode.OK, Link);
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png });
        var action = new UploadImageAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);
        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Handler.RequestedUris);
        Assert.Equal(2, harness.Paste.Calls);
        Assert.Equal(2, harness.Backend.Writes.Count);
        Assert.Equal(Link, harness.Backend.Writes[1].Text);
    }

    [Fact(DisplayName = "example 5: CanExecute(text \"hello\") => false;  CanExecute(empty image) => false")]
    public void Example5()
    {
        using var harness = new ActionTestHarness();
        var text = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });
        var emptyImage = harness.AddToHistory(new ClipboardSnapshot { Image = [] });
        var action = new UploadImageAction();

        Assert.False(action.CanExecute(text));
        Assert.False(action.CanExecute(emptyImage));
    }

    [Fact(DisplayName = "example 6: Catbox answers 412 \"File too large\" => HttpRequestException; nothing pasted, nothing added to the history")]
    public async Task Example6()
    {
        using var harness = new ActionTestHarness();
        Answer(harness, HttpStatusCode.PreconditionFailed, "File too large");
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = Png });
        var historyCount = harness.History.Items.Count;
        var action = new UploadImageAction();

        await Assert.ThrowsAsync<HttpRequestException>(
            () => action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct));

        Assert.Empty(harness.Backend.Writes);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Equal(historyCount, harness.History.Items.Count);
    }
}
