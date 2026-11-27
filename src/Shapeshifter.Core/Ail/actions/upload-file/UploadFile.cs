using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

using System.Collections.Concurrent;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that uploads a single copied file to Catbox and pastes a public link to it.
/// </summary>
public sealed class UploadFileAction : IClipboardAction
{
    /// <summary>
    /// The content type every file is sent with, so Catbox serves each type itself.
    /// </summary>
    public const string MediaType = "application/octet-stream";

    private readonly ConcurrentDictionary<object, string> _links = new();

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "upload-file";

    /// <summary>
    /// The action's position among the actions.
    /// </summary>
    public int Order => 78;

    /// <summary>
    /// Gets the title shown for this action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUploadText;
    }

    /// <summary>
    /// Gets the description shown for this action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description naming the upload service.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionUploadFileDescription, Catbox.Name);
    }

    /// <summary>
    /// Tells whether the item is one local, existing, non-empty file small enough for Catbox.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns><see langword="true"/> when the file can be uploaded.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Kind != ClipboardContentKind.File)
        {
            return false;
        }

        var files = item.Snapshot.Files;
        if (files.Count != 1)
        {
            return false;
        }

        var path = files[0];
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (!File.Exists(path))
        {
            return false;
        }

        var length = new FileInfo(path).Length;
        return length >= 1 && length <= Catbox.MaxBytes;
    }

    /// <summary>
    /// Uploads the file once per item, then pastes its link and adds the link to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the upload and the paste.</param>
    /// <returns>A task that completes when the link has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var uploaded = false;
        if (!_links.TryGetValue(item.Id, out var link))
        {
            var path = item.Snapshot.Files[0];
            var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            link = await Catbox.UploadAsync(context.Http, data, Path.GetFileName(path), MediaType, cancellationToken).ConfigureAwait(false);
            _links[item.Id] = link;
            uploaded = true;
        }

        var snapshot = new ClipboardSnapshot { Text = link };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        context.History.Add(snapshot);

        if (uploaded)
        {
            context.Logger.Information("Uploaded a file to {Service}", Catbox.Name);
        }
    }
}
