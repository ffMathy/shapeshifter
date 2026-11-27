using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Pastes a text item without the white space at its start and end, keeping its inner layout.
/// </summary>
public sealed class PasteTrimmedAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-trimmed";

    /// <summary>Where the action appears among the actions.</summary>
    public int Order => 24;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmed;
    }

    /// <summary>Gets the action's description.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteTrimmedDescription;
    }

    /// <summary>
    /// Tells whether the item is text that holds something other than white space
    /// and starts or ends with white space.
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

        return text.Trim().Length != text.Length;
    }

    /// <summary>
    /// Pastes the item's plain text with the white space at its start and end removed,
    /// without adding it to the history.
    /// </summary>
    /// <param name="item">The item to paste.</param>
    /// <param name="context">The context to paste with.</param>
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
