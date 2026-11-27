using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that opens the folder holding a copied file in the system's file manager, with the file selected.
/// </summary>
public sealed class ShowInFileManagerAction : IClipboardAction
{
    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "show-in-file-manager";

    /// <summary>
    /// Where this action is placed among the actions.
    /// </summary>
    public int Order => 232;

    /// <summary>
    /// Gets the title shown for this action.
    /// </summary>
    /// <param name="item">The item the action would act on.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionShowInFileManager;
    }

    /// <summary>
    /// Gets the description shown for this action, worded for one file or several.
    /// </summary>
    /// <param name="item">The item the action would act on.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.FileCollection
            ? Strings.ActionShowInFileManagerDescription
            : Strings.ActionShowFileInFileManagerDescription;
    }

    /// <summary>
    /// Tells whether the item holds files whose first path is local and still exists.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the first file can be shown in the file manager.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.File && item.Kind != ClipboardContentKind.FileCollection)
        {
            return false;
        }

        var files = item.Snapshot.Files;
        if (files.Count == 0)
        {
            return false;
        }

        var path = files[0];
        if (path.StartsWith(@"\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        return File.Exists(path) || Directory.Exists(path);
    }

    /// <summary>
    /// Reveals the item's first file in the system's file manager; nothing is pasted and the clipboard is unchanged.
    /// </summary>
    /// <param name="item">The item whose first file is shown.</param>
    /// <param name="context">The context giving access to the launcher.</param>
    /// <param name="cancellationToken">The token to cancel the action.</param>
    /// <returns>A task that completes when the file manager has been asked to show the file.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        context.Launcher.RevealInFileManager(item.Snapshot.Files[0]);
        return Task.CompletedTask;
    }
}
