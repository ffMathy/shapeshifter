using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes where the copied files are as text, one full path per line.
/// </summary>
public sealed class PastePathsAction : IClipboardAction
{
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-paths";

    /// <summary>
    /// The action's position among the actions.
    /// </summary>
    public int Order => 70;

    /// <summary>
    /// Gets the title, which says path for one file and paths for several.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title shown for the action.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File ? Strings.ActionPastePath : Strings.ActionPastePaths;
    }

    /// <summary>
    /// Gets the description, which speaks of one file or of several.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description shown for the action.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.File
            ? Strings.ActionPastePathDescription
            : Strings.ActionPastePathsDescription;
    }

    /// <summary>
    /// Tells whether the item holds one or several files.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is a file or a collection of files.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind is ClipboardContentKind.File or ClipboardContentKind.FileCollection;
    }

    /// <summary>
    /// Pastes the item's file paths as text without adding them to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paths are pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var snapshot = new ClipboardSnapshot { Text = Join(item.Snapshot.Files) };
        await context.WriteAndPasteAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Joins paths as they were copied, in their order, by the system's line ending, with none after the last.
    /// </summary>
    /// <param name="paths">The full paths of the files.</param>
    /// <returns>The paths as text, one per line.</returns>
    public static string Join(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return string.Join(OperatingSystem.IsWindows() ? "\r\n" : "\n", paths);
    }
}
