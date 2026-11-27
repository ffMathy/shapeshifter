using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that uploads a text item to Catbox and pastes a public link to it,
/// so long text can be shared where only a link fits.
/// </summary>
public sealed class UploadTextAction : IClipboardAction
{
    /// <summary>The media type the uploaded text file is sent with.</summary>
    public const string MediaType = "text/plain; charset=utf-8";

    private static readonly ConcurrentDictionary<object, string> UploadedLinks = new();

    /// <summary>The action's stable identifier.</summary>
    public string Id => "upload-text";

    /// <summary>Where the action appears among the actions.</summary>
    public int Order => 35;

    /// <summary>Gives the action's title, the same as the image upload's.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUploadText;
    }

    /// <summary>Gives the action's description, naming the host the text is uploaded to.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionUploadTextDescription, Catbox.Name);
    }

    /// <summary>Tells whether the item is text holding something other than whitespace.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item's text can be uploaded.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && !string.IsNullOrWhiteSpace(item.Snapshot.Text);
    }

    /// <summary>
    /// Uploads the item's plain text to Catbox once, then pastes the link and adds it to the history.
    /// </summary>
    /// <param name="item">The text item to upload.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the upload and the paste.</param>
    /// <returns>A task that completes when the link has been pasted.</returns>
    public async System.Threading.Tasks.Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        object key = item.Id;
        if (!UploadedLinks.TryGetValue(key, out var link))
        {
            var text = item.Snapshot.Text ?? string.Empty;
            var preamble = Encoding.UTF8.GetPreamble();
            var body = Encoding.UTF8.GetBytes(text);
            var data = new byte[preamble.Length + body.Length];
            preamble.CopyTo(data, 0);
            body.CopyTo(data, preamble.Length);

            var fileName = "Text "
                + item.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture)
                + ".txt";

            link = await Catbox.UploadAsync(context.Http, data, fileName, MediaType, cancellationToken).ConfigureAwait(false);
            UploadedLinks[key] = link;
            context.Logger.Information("Uploaded a text to {Service}", Catbox.Name);
        }

        var snapshot = new ClipboardSnapshot { Text = link };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        context.History.Add(snapshot);
    }
}
