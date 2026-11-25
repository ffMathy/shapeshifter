using System.Net;
using System.Text;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;

namespace Shapeshifter.Core.Tests.Actions;

public sealed class UploadTextActionTests
{
    private const string Link = "https://files.catbox.moe/abc123.txt";

    public static readonly TheoryData<string, bool> CanExecuteMatrix = new()
    {
        { "plain", true },
        { "text+html", true },
        { "link", true },
        { "image-link", true },
        { "text+image", true },
        { "whitespace", false },
        { "html-only", false },
        { "empty-text+html", false },
        { "rtf-only", false },
        { "image", false },
        { "file", false },
        { "files", false },
        { "file+html", false },
        { "other", false },
        { "empty", false },
    };

    [Theory]
    [MemberData(nameof(CanExecuteMatrix))]
    public void CanExecute_OnlyForTextWithSomethingInIt(string kind, bool expected) =>
        Assert.Equal(expected, new UploadTextAction().CanExecute(Items.ByName(kind)));

    [Fact]
    public void Description_NamesCatbox() =>
        Assert.Equal("Uploads this text to Catbox and pastes a public link to it.", new UploadTextAction().GetDescription(Items.ByName("plain")));

    [Fact]
    public async Task Execute_UploadsTheTextToCatboxAsATxtFile_AndPastesTheLink()
    {
        using var harness = new ActionTestHarness();
        byte[]? body = null;
        harness.Handler.Respond = request =>
        {
            body = request.Content!.ReadAsByteArrayAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Link + "\n") };
        };
        var capturedAt = new DateTimeOffset(2026, 11, 20, 13, 5, 9, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 20, 13, 5, 9)));
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "héllo wörld" });
        item.CapturedAt = capturedAt;

        await new UploadTextAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Equal(Catbox.Endpoint, Assert.Single(harness.Handler.RequestedUris));
        var form = Encoding.UTF8.GetString(body!);
        Assert.Contains("name=reqtype", form, StringComparison.Ordinal);
        Assert.Contains("fileupload", form, StringComparison.Ordinal);
        Assert.Contains("name=fileToUpload", form, StringComparison.Ordinal);
        Assert.Contains("Text 2026-11-20 13.05.09.txt", form, StringComparison.Ordinal);
        Assert.Contains("text/plain; charset=utf-8", form, StringComparison.Ordinal);
        var text = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("héllo wörld")).ToArray();
        Assert.True(body!.AsSpan().IndexOf(text) >= 0, "the file is the text in UTF-8, after a byte order mark");

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal(Link, written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(Link, harness.History.Items[0].Snapshot.Text);
    }

    [Fact]
    public async Task Execute_TwiceForTheSameItem_UploadsOnce()
    {
        using var harness = new ActionTestHarness();
        harness.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Link) };
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });
        var action = new UploadTextAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);
        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        Assert.Single(harness.Handler.RequestedUris);
        Assert.Equal(2, harness.Paste.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, "Error: something went wrong")]
    [InlineData(HttpStatusCode.OK, "http://files.catbox.moe/abc.txt")]
    [InlineData(HttpStatusCode.OK, "")]
    [InlineData(HttpStatusCode.PreconditionFailed, "Invalid uploader")]
    [InlineData(HttpStatusCode.InternalServerError, Link)]
    public async Task Execute_WhenCatboxReturnsNoLink_FailsAndPastesNothing(HttpStatusCode status, string response)
    {
        using var harness = new ActionTestHarness();
        harness.Handler.Respond = _ => new HttpResponseMessage(status) { Content = new StringContent(response) };
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        await Assert.ThrowsAsync<HttpRequestException>(() => new UploadTextAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct));

        Assert.Empty(harness.Backend.Writes);
        Assert.Equal(0, harness.Paste.Calls);
    }
}
