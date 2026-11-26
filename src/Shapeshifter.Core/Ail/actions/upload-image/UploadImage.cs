using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Collections.Concurrent;
using System.Globalization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that uploads an image item to Catbox and pastes a public link to it.
/// </summary>
public sealed class UploadImageAction : IClipboardAction
{
    private readonly ConcurrentDictionary<object, string> _links = new();

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "upload-image";

    /// <summary>
    /// The action's position among the actions.
    /// </summary>
    public int Order => 30;

    /// <summary>
    /// Gets the action's title, shown in the actions list.
    /// </summary>
    /// <param name="item">The item the action would apply to.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUploadImage;
    }

    /// <summary>
    /// Gets the action's description, naming the service the image is uploaded to.
    /// </summary>
    /// <param name="item">The item the action would apply to.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionUploadImageDescription, Catbox.Name);
    }

    /// <summary>
    /// Tells whether the item is an image that can be uploaded.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns><see langword="true"/> when the item is a non-empty image.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Image && item.Snapshot.Image is { Length: > 0 };
    }

    /// <summary>
    /// Uploads the item's image to Catbox once, pastes the public link, and adds the link to the history.
    /// </summary>
    /// <param name="item">The image item to upload.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the upload and the paste.</param>
    /// <returns>A task that completes when the link has been pasted and added to the history.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        if (!_links.TryGetValue(item.Id, out var link))
        {
            var image = item.Snapshot.Image!;
            var time = item.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH.mm.ss", CultureInfo.InvariantCulture);
            var fileName = $"Image {time}.{ImageFormat.GetExtension(image)}";
            link = await Catbox.UploadAsync(context.Http, image, fileName, ImageFormat.GetMediaType(image), cancellationToken)
                .ConfigureAwait(false);
            _links[item.Id] = link;
            context.Logger.Information("Uploaded an image to {Service}", Catbox.Name);
        }

        var snapshot = new ClipboardSnapshot { Text = link };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
        context.History.Add(snapshot);
    }
}
