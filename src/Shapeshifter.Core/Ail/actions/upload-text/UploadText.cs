using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that uploads a text item to Catbox and pastes a public link to it.
/// </summary>
public sealed class UploadTextAction : IClipboardAction
{
    /// <summary>
    /// The content type the uploaded text file is sent with.
    /// </summary>
    public const string MediaType = "text/plain; charset=utf-8";

    private readonly ConcurrentDictionary<object, string> _links = new();

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "upload-text";

    /// <summary>
    /// Where the action appears among the actions.
    /// </summary>
    public int Order => 35;

    /// <summary>
    /// Gives the action's title for the item.
    /// </summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUploadText;
    }

    /// <summary>
    /// Gives the action's description for the item.
    /// </summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized description, naming the upload service.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionUploadTextDescription, Catbox.Name);
    }

    /// <summary>
    /// Tells whether the item is text holding something other than whitespace.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item's text can be uploaded.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && !string.IsNullOrWhiteSpace(item.Snapshot.Text);
    }

    /// <summary>
    /// Uploads the item's text to Catbox once, then pastes the link and adds it to the history.
    /// </summary>
    /// <param name="item">The text item to upload.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the upload and the paste.</param>
    /// <returns>A task that completes when the link has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        object key = item.Id;
        if (!_links.TryGetValue(key, out var link))
        {
            var text = item.Snapshot.Text ?? string.Empty;
            byte[] bom = [0xEF, 0xBB, 0xBF];
            byte[] data = [.. bom, .. Encoding.UTF8.GetBytes(text)];
            var fileName = "Text " + item.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture) + ".txt";
            link = await Catbox.UploadAsync(context.Http, data, fileName, MediaType, cancellationToken).ConfigureAwait(false);
            _links[key] = link;
        }

        var snapshot = new ClipboardSnapshot { Text = link };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        context.History.Add(snapshot);
        context.Logger.Information("Uploaded a text to {Service}", Catbox.Name);
    }
}
