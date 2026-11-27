using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Uploads an image or a text to Catbox anonymously and returns the public link to it.
/// </summary>
public static class Catbox
{
    /// <summary>
    /// The longest part of an answer body shown in an error message.
    /// </summary>
    private const int MaxShownBodyLength = 200;

    /// <summary>
    /// The name of the host uploads go to.
    /// </summary>
    public const string Name = "Catbox";

    /// <summary>
    /// The largest upload Catbox accepts, in bytes: 200 MB.
    /// </summary>
    public const int MaxBytes = 200 * 1024 * 1024;

    /// <summary>
    /// The address every upload is posted to.
    /// </summary>
    public const string Endpoint = "https://catbox.moe/user/api.php";

    /// <summary>
    /// How long an upload may take before it is cancelled; a check may set it shorter, and sets it back after.
    /// </summary>
    public static System.TimeSpan Timeout { get; set; } = System.TimeSpan.FromMinutes(2);

    /// <summary>
    /// Uploads the data as a file to Catbox and returns its public link, an absolute https address.
    /// </summary>
    /// <param name="http">The shared HTTP client that sends the request.</param>
    /// <param name="data">The content of the file to upload.</param>
    /// <param name="fileName">The name the file is uploaded under.</param>
    /// <param name="mediaType">The file's content type, sent exactly as given.</param>
    /// <param name="cancellation">Cancels the upload.</param>
    /// <returns>The public link to the uploaded file.</returns>
    public static async System.Threading.Tasks.Task<string> UploadAsync(
        System.Net.Http.HttpClient http,
        byte[] data,
        string fileName,
        string mediaType,
        System.Threading.CancellationToken cancellation)
    {
        System.ArgumentNullException.ThrowIfNull(http);
        System.ArgumentNullException.ThrowIfNull(data);
        System.ArgumentNullException.ThrowIfNull(fileName);
        System.ArgumentNullException.ThrowIfNull(mediaType);

        if (data.Length > MaxBytes)
        {
            throw new System.InvalidOperationException("The file is larger than the 200 MB Catbox accepts.");
        }

        using var timeout = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(Timeout);

        using var form = new System.Net.Http.MultipartFormDataContent();
        form.Add(new System.Net.Http.StringContent("fileupload"), "reqtype");
        var file = new System.Net.Http.ByteArrayContent(data);
        file.Headers.TryAddWithoutValidation("Content-Type", mediaType);
        form.Add(file, "fileToUpload", fileName);

        using var response = await http.PostAsync(Endpoint, form, timeout.Token).ConfigureAwait(false);
        var body = (await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false)).Trim();

        if (!response.IsSuccessStatusCode)
        {
            throw new System.Net.Http.HttpRequestException(
                $"Catbox refused the upload ({(int)response.StatusCode}): {Shorten(body)}",
                null,
                response.StatusCode);
        }

        if (!System.Uri.TryCreate(body, System.UriKind.Absolute, out var link) || link.Scheme != System.Uri.UriSchemeHttps)
        {
            throw new System.Net.Http.HttpRequestException($"Catbox did not return a link: {Shorten(body)}");
        }

        return body;
    }

    /// <summary>
    /// Shortens an answer body to its first 200 characters, followed by "…", when it is longer.
    /// </summary>
    private static string Shorten(string body) =>
        body.Length > MaxShownBodyLength ? string.Concat(body.AsSpan(0, MaxShownBodyLength), "…") : body;
}
