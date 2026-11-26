using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;

namespace Shapeshifter.Core.Actions;

/// <summary>All available actions, in display order.</summary>
public sealed class ActionCatalog
{
    public ActionCatalog(IEnumerable<IClipboardAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        All = actions.OrderBy(action => action.Order).ToArray();
    }

    public static ActionCatalog CreateDefault() => new(
    [
        new PasteAction(),
        new PasteAsFileAction(),
        new PasteAsPlainTextAction(),
        new PasteTrimmedAction(),
        new UploadImageAction(),
        new UploadTextAction(),
        new PasteAsZipAction(),
        new CopyImageFromLinkAction(),
        new OpenLinksAction(),
        new DecodeBase64Action(),
        new UrlDecodeAction(),
        new ReformatJsonAction(),
        new PasteAsMarkdownAction(),
        new PasteAsHexColorAction(),
        new PasteAsRgbColorAction(),
        new PasteAsHslColorAction(),
        new PasteAsDateAction(),
        new PasteAsUnixTimeAction(),
        new PasteResultAction(),
        new WriteEmailAction(),
        new SearchWebAction(),
        new CopyFileAtPathAction(),
        new ShowPathInFileManagerAction(),
        new PastePathsAction(),
        new TogglePinAction(),
    ]);

    public IReadOnlyList<IClipboardAction> All { get; }

    public IReadOnlyList<IClipboardAction> GetAvailable(ClipboardItem item) =>
        All.Where(action => action.CanExecute(item)).ToArray();
}
