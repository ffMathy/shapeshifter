using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Net.Http;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Uploads an image or a text to Catbox anonymously, to get a public link that can be pasted.
/// </summary>
public static class Catbox
{
    /// <summary>
    /// The name of the upload host.
    /// </summary>
    public const string Name = "Catbox";

    /// <summary>
    /// The largest upload Catbox accepts, in bytes (200 MB).
    /// </summary>
    public const int MaxBytes = 200 * 1024 * 1024;

    /// <summary>
    /// The address every upload is posted to.
    /// </summary>
    public const string Endpoint = "https://catbox.moe/user/api.php";

    private const int MaxShownBodyLength = 200;

    /// <summary>
    /// How long an upload may take before it is cancelled; a check may set it shorter, and sets it back after.
    /// </summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Uploads the data to Catbox and returns its public link, an absolute https address.
    /// </summary>
    /// <param name="http">The shared HTTP client, which sends Shapeshifter's User-Agent.</param>
    /// <param name="data">The bytes to upload.</param>
    /// <param name="fileName">The name the file is uploaded under.</param>
    /// <param name="mediaType">The file's content type, sent exactly as given.</param>
    /// <param name="cancellation">Cancels the upload.</param>
    /// <returns>The public link to the uploaded file.</returns>
    public static async Task<string> UploadAsync(
        HttpClient http,
        byte[] data,
        string fileName,
        string mediaType,
        CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(mediaType);

        if (data.LongLength > MaxBytes)
        {
            throw new InvalidOperationException("The file is larger than the 200 MB Catbox accepts.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(Timeout);
        var token = timeout.Token;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("fileupload"), "reqtype");
        var file = new ByteArrayContent(data);
        file.Headers.TryAddWithoutValidation("Content-Type", mediaType);
        form.Add(file, "fileToUpload", fileName);

        using var response = await http.PostAsync(Endpoint, form, token).ConfigureAwait(false);
        var body = (await response.Content.ReadAsStringAsync(token).ConfigureAwait(false)).Trim();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Catbox refused the upload ({(int)response.StatusCode}): {Shorten(body)}",
                null,
                response.StatusCode);
        }

        if (!Uri.TryCreate(body, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps)
        {
            throw new HttpRequestException($"Catbox did not return a link: {Shorten(body)}");
        }

        return body;
    }

    private static string Shorten(string body) =>
        body.Length > MaxShownBodyLength ? string.Concat(body.AsSpan(0, MaxShownBodyLength), "…") : body;
}
