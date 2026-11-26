// ail-derived-from: Catbox@a4956ed794aa3e34
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the Catbox upload node.</summary>
public sealed class CatboxTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<CancellationToken, Task<HttpResponseMessage>> _respond;

        public FakeHandler(Func<CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

        public int RequestCount { get; private set; }

        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            return await _respond(cancellationToken);
        }
    }

    private static FakeHandler Answering(HttpStatusCode status, string body) =>
        new(_ => Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }));

    private static async Task<string> UploadAsync(FakeHandler handler, byte[] data, string fileName, string mediaType)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        return await Catbox.UploadAsync(http, data, fileName, mediaType, TestContext.Current.CancellationToken);
    }

    [Fact(DisplayName = "example 1: data of 3 bytes, \"a.png\", \"image/png\"; Catbox answers 200 \"https://files.catbox.moe/abc123.png\\n\" => \"https://files.catbox.moe/abc123.png\"")]
    public async Task Example01ReturnsTrimmedLink()
    {
        using var handler = Answering(HttpStatusCode.OK, "https://files.catbox.moe/abc123.png\n");

        var link = await UploadAsync(handler, [1, 2, 3], "a.png", "image/png");

        Assert.Equal("https://files.catbox.moe/abc123.png", link);
    }

    [Fact(DisplayName = "example 2: \"a.txt\" with mediaType \"text/plain; charset=utf-8\" => the file's content type is sent as text/plain; charset=utf-8")]
    public async Task Example02SendsMediaTypeExactly()
    {
        using var handler = Answering(HttpStatusCode.OK, "https://files.catbox.moe/abc123.txt");

        await UploadAsync(handler, [104, 105], "a.txt", "text/plain; charset=utf-8");

        Assert.Single(handler.Bodies);
        Assert.Contains("Content-Type: text/plain; charset=utf-8", handler.Bodies[0], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "example 3: Catbox answers 412 \"File too large\" => HttpRequestException \"Catbox refused the upload (412): File too large\", with status code 412")]
    public async Task Example03RefusalCarriesStatus()
    {
        using var handler = Answering(HttpStatusCode.PreconditionFailed, "File too large");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox refused the upload (412): File too large", ex.Message);
        Assert.Equal(HttpStatusCode.PreconditionFailed, ex.StatusCode);
    }

    [Fact(DisplayName = "example 4: Catbox answers 500 \"https://files.catbox.moe/a.png\" => HttpRequestException \"Catbox refused the upload (500): https://files.catbox.moe/a.png\"")]
    public async Task Example04ServerErrorWithLinkIsRefusal()
    {
        using var handler = Answering(HttpStatusCode.InternalServerError, "https://files.catbox.moe/a.png");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox refused the upload (500): https://files.catbox.moe/a.png", ex.Message);
    }

    [Fact(DisplayName = "example 5: Catbox answers 200 \"error: something\" => HttpRequestException \"Catbox did not return a link: error: something\"")]
    public async Task Example05NonLinkBody()
    {
        using var handler = Answering(HttpStatusCode.OK, "error: something");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox did not return a link: error: something", ex.Message);
    }

    [Fact(DisplayName = "example 6: Catbox answers 200 \"http://files.catbox.moe/a.png\" => HttpRequestException \"Catbox did not return a link: http://files.catbox.moe/a.png\"")]
    public async Task Example06HttpLinkIsNotALink()
    {
        using var handler = Answering(HttpStatusCode.OK, "http://files.catbox.moe/a.png");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox did not return a link: http://files.catbox.moe/a.png", ex.Message);
    }

    [Fact(DisplayName = "example 7: Catbox answers 200 \"\" => HttpRequestException \"Catbox did not return a link: \"")]
    public async Task Example07EmptyBody()
    {
        using var handler = Answering(HttpStatusCode.OK, "");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox did not return a link: ", ex.Message);
    }

    [Fact(DisplayName = "example 8: an answer body of 250 characters in an error => the message shows its first 200 characters, then \"…\"")]
    public async Task Example08LongBodyIsShortened()
    {
        var body = new string('x', 250);
        using var handler = Answering(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

        Assert.Equal("Catbox did not return a link: " + new string('x', 200) + "…", ex.Message);
    }

    [Fact(DisplayName = "example 9: data of 201 MB => InvalidOperationException \"The file is larger than the 200 MB Catbox accepts.\", and no request is sent")]
    public async Task Example09TooLargeIsRefusedBeforeSending()
    {
        using var handler = Answering(HttpStatusCode.OK, "https://files.catbox.moe/abc123.png");
        var data = new byte[201 * 1024 * 1024];

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => UploadAsync(handler, data, "a.png", "image/png"));

        Assert.Equal("The file is larger than the 200 MB Catbox accepts.", ex.Message);
        Assert.Equal(0, handler.RequestCount);
    }

    [Fact(DisplayName = "example 10: Catbox.Timeout set to 200 ms, and no answer => the upload is cancelled with OperationCanceledException after about 200 ms")]
    public async Task Example10TimeoutCancelsUpload()
    {
        using var handler = new FakeHandler(async ct =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var original = Catbox.Timeout;
        Catbox.Timeout = TimeSpan.FromMilliseconds(200);
        try
        {
            var stopwatch = Stopwatch.StartNew();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => UploadAsync(handler, [1, 2, 3], "a.png", "image/png"));

            stopwatch.Stop();
            Assert.InRange(stopwatch.Elapsed, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(5));
        }
        finally
        {
            Catbox.Timeout = original;
        }
    }
}
