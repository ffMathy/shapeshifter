using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes the full paths of copied files as text, one per line, without adding them to the history.
/// </summary>
public sealed class PastePathsAction : IClipboardAction
{
    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "paste-paths";

    /// <summary>
    /// Where the action appears among the actions.
    /// </summary>
    public int Order => 70;

    /// <summary>
    /// Returns the title: "Paste path" for one file, otherwise "Paste paths".
    /// </summary>
    /// <param name="item">The item the action would act on.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File ? Strings.ActionPastePath : Strings.ActionPastePaths;
    }

    /// <summary>
    /// Returns the description, worded for one file or for several.
    /// </summary>
    /// <param name="item">The item the action would act on.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            ? Strings.ActionPastePathDescription
            : Strings.ActionPastePathsDescription;
    }

    /// <summary>
    /// Returns whether the item holds one or several files.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is a file or a file collection.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File || item.Kind == ClipboardContentKind.FileCollection;
    }

    /// <summary>
    /// Pastes the item's file paths as text, one per line; nothing is added to the history.
    /// </summary>
    /// <param name="item">The item whose file paths are pasted.</param>
    /// <param name="context">The context used to write the clipboard and paste.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paths have been pasted.</returns>
    public System.Threading.Tasks.Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(context);
        var snapshot = new ClipboardSnapshot { Text = Join(item.Snapshot.Files) };
        return context.WriteAndPasteAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Joins the paths, as copied and in their order, with the system's line ending and none after the last.
    /// </summary>
    /// <param name="paths">The file paths to join.</param>
    /// <returns>The paths, one per line.</returns>
    public static string Join(System.Collections.Generic.IReadOnlyList<string> paths)
    {
        System.ArgumentNullException.ThrowIfNull(paths);
        return string.Join(System.Environment.NewLine, paths);
    }
}
