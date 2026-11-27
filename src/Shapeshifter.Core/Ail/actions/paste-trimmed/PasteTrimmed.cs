using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that pastes a text item without the white space at its start and end.
/// </summary>
public sealed class PasteTrimmedAction : IClipboardAction
{
    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "paste-trimmed";

    /// <summary>
    /// Where the action appears in the list of actions.
    /// </summary>
    public int Order => 24;

    /// <summary>
    /// Returns the action's title, as shown in the list of actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmed;
    }

    /// <summary>
    /// Returns the action's description, as shown in the list of actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmedDescription;
    }

    /// <summary>
    /// Tells whether the item is text that holds something other than white space and starts or ends with white space.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when trimming would change the item's text.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        var text = item.Snapshot.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]);
    }

    /// <summary>
    /// Pastes a new snapshot holding only the item's text without the white space at its start and end, without adding it to the history.
    /// </summary>
    /// <param name="item">The item whose text is pasted.</param>
    /// <param name="context">The context to paste through.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var trimmed = (item.Snapshot.Text ?? string.Empty).Trim();
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = trimmed }, cancellationToken);
    }
}
