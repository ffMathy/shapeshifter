using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes a text item without the white space at its start and end.
/// </summary>
public sealed class PasteTrimmedAction : IClipboardAction
{
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-trimmed";

    /// <summary>
    /// Where the action appears in the list of actions.
    /// </summary>
    public int Order => 24;

    /// <summary>
    /// Gets the action's title shown for the given item.
    /// </summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmed;
    }

    /// <summary>
    /// Gets the action's description shown for the given item.
    /// </summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmedDescription;
    }

    /// <summary>
    /// Tells whether the item is text that trimming would change.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is text with non-white-space content that starts or ends with white space.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        string? text = item.Snapshot.Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return char.IsWhiteSpace(text[0]) || char.IsWhiteSpace(text[^1]);
    }

    /// <summary>
    /// Pastes the item's text trimmed at its start and end, without adding it to the history.
    /// </summary>
    /// <param name="item">The item to paste.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        string trimmed = (item.Snapshot.Text ?? string.Empty).Trim();
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = trimmed }, cancellationToken);
    }
}
