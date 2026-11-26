using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Uploads an image or a text to Catbox anonymously and returns the public link to it.
/// </summary>
public static class Catbox
{
    /// <summary>The name of the host the upload goes to.</summary>
    public const string Name = "Catbox";

    /// <summary>The largest upload Catbox accepts, in bytes (200 MB).</summary>
    public const int MaxBytes = 200 * 1024 * 1024;

    /// <summary>The address every upload is posted to.</summary>
    public const string Endpoint = "https://catbox.moe/user/api.php";

    private const int MaxShownBodyLength = 200;

    /// <summary>How long one upload may take before it is cancelled; two minutes unless a check sets it shorter.</summary>
    public static TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Uploads the data as a file named <paramref name="fileName"/> with the content type
    /// <paramref name="mediaType"/>, and returns the public link, an absolute https address.
    /// </summary>
    /// <param name="http">The shared client that sends the request.</param>
    /// <param name="data">The content of the file to upload.</param>
    /// <param name="fileName">The name the file is uploaded under.</param>
    /// <param name="mediaType">The content type of the file, sent exactly as given.</param>
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

        if (data.Length > MaxBytes)
        {
            throw new InvalidOperationException("The file is larger than the 200 MB Catbox accepts.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(Timeout);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("fileupload"), "reqtype");
        var file = new ByteArrayContent(data);
        file.Headers.TryAddWithoutValidation("Content-Type", mediaType);
        form.Add(file, "fileToUpload", fileName);

        using var response = await http.PostAsync(new Uri(Endpoint), form, timeout.Token).ConfigureAwait(false);
        var body = (await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false)).Trim();

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Catbox refused the upload ({(int)response.StatusCode}): {Shown(body)}",
                null,
                response.StatusCode);
        }

        if (!Uri.TryCreate(body, UriKind.Absolute, out var link) || link.Scheme != Uri.UriSchemeHttps)
        {
            throw new HttpRequestException($"Catbox did not return a link: {Shown(body)}");
        }

        return body;
    }

    private static string Shown(string body) =>
        body.Length > MaxShownBodyLength ? string.Concat(body.AsSpan(0, MaxShownBodyLength), "…") : body;
}
