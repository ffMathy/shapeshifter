using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

using System.Collections.Concurrent;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that uploads a copied file to Catbox and pastes a public link to it,
/// so a file can be shared without a file sharing website or an account.
/// </summary>
public sealed class UploadFileAction : IClipboardAction
{
    /// <summary>The content type every uploaded file is sent with; Catbox serves each type itself.</summary>
    public const string MediaType = "application/octet-stream";

    private static readonly ConcurrentDictionary<object, string> UploadedLinks = new();

    /// <summary>The action's stable identifier.</summary>
    public string Id => "upload-file";

    /// <summary>Where the action appears among the actions.</summary>
    public int Order => 78;

    /// <summary>Gets the title shown for this action, the same as the other uploads.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUploadText;
    }

    /// <summary>Gets the description shown for this action, naming the upload service.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionUploadFileDescription, Catbox.Name);
    }

    /// <summary>
    /// Tells whether the item is one existing local file, not a folder, of 1 byte to Catbox's limit.
    /// </summary>
    /// <param name="item">The item to check.</param>
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

        long length = new FileInfo(path).Length;
        return length >= 1 && length <= Catbox.MaxBytes;
    }

    /// <summary>
    /// Uploads the file once, then pastes the link and adds it to the history as the newest item.
    /// </summary>
    /// <param name="item">The file item to upload.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the upload and the paste.</param>
    /// <returns>A task that completes when the link has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        object key = item.Id;
        if (!UploadedLinks.TryGetValue(key, out var link))
        {
            var path = item.Snapshot.Files[0];
            var data = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            link = await Catbox.UploadAsync(context.Http, data, Path.GetFileName(path), MediaType, cancellationToken).ConfigureAwait(false);
            UploadedLinks[key] = link;
            context.Logger.Information("Uploaded a file to {Service}", Catbox.Name);
        }

        var snapshot = new ClipboardSnapshot { Text = link };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        context.History.Add(snapshot);
    }
}
