using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Recognises copied text that names an existing file or folder on this computer.
/// </summary>
public static class FilePath
{
    private const int MaxLength = 1024;
    private const string FileScheme = "file://";

    /// <summary>
    /// Gives the full path of the existing local file or folder the text names, or null when it names none.
    /// </summary>
    /// <param name="text">The copied text, or null.</param>
    /// <returns>The full path as the system writes it, or null.</returns>
    public static string? Resolve(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var candidate = text.Trim();
        if (candidate.Length >= 2
            && (candidate[0] == '"' || candidate[0] == '\'')
            && candidate[^1] == candidate[0])
        {
            candidate = candidate[1..^1];
        }

        if (candidate.Contains('\n', StringComparison.Ordinal)
            || candidate.Contains('\r', StringComparison.Ordinal)
            || candidate.Length > MaxLength)
        {
            return null;
        }

        string path;
        if (candidate.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            var rest = candidate[FileScheme.Length..];
            if (!rest.StartsWith('/'))
            {
                return null;
            }

            path = Uri.UnescapeDataString(rest);
            if (OperatingSystem.IsWindows()
                && path.Length >= 3
                && char.IsAsciiLetter(path[1])
                && path[2] == ':')
            {
                path = path[1..];
            }
        }
        else if (candidate == "~")
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (candidate.StartsWith("~/", StringComparison.Ordinal)
            || candidate.StartsWith("~\\", StringComparison.Ordinal))
        {
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + candidate[1..];
        }
        else
        {
            path = candidate;
        }

        if (path.StartsWith("\\\\", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        if (!IsAbsolute(path))
        {
            return null;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return null;
        }

        return Path.GetFullPath(path);
    }

    private static bool IsAbsolute(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return path.Length >= 3
                && char.IsAsciiLetter(path[0])
                && path[1] == ':'
                && (path[2] == '\\' || path[2] == '/');
        }

        return path.StartsWith('/');
    }
}

/// <summary>
/// The action that puts the file or folder a copied path names on the clipboard, and in the history.
/// </summary>
public sealed class CopyFileAtPathAction : IClipboardAction
{
    /// <summary>The action's stable id.</summary>
    public string Id => "copy-file-at-path";

    /// <summary>Where the action sits among the actions.</summary>
    public int Order => 110;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPath;
    }

    /// <summary>Gives the action's description.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPathDescription;
    }

    /// <summary>Tells whether the item is text naming an existing local file or folder.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Puts the file or folder on the clipboard and adds it to the history, without pasting.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">What the action may use.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the file is copied.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var path = FilePath.Resolve(item.Snapshot.Text);
        if (path is null)
        {
            return Task.CompletedTask;
        }

        return context.CopyNewContentAsync(new ClipboardSnapshot { Files = [path] }, cancellationToken);
    }
}

/// <summary>
/// The action that opens the folder holding the file or folder a copied path names, with it selected.
/// </summary>
public sealed class ShowPathInFileManagerAction : IClipboardAction
{
    /// <summary>The action's stable id.</summary>
    public string Id => "show-path-in-file-manager";

    /// <summary>Where the action sits among the actions.</summary>
    public int Order => 230;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionShowInFileManager;
    }

    /// <summary>Gives the action's description.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionShowPathInFileManagerDescription;
    }

    /// <summary>Tells whether the item is text naming an existing local file or folder.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Reveals the file or folder in the system's file manager; the clipboard and history stay as they are.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">What the action may use.</param>
    /// <param name="cancellationToken">Not used; revealing is immediate.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var path = FilePath.Resolve(item.Snapshot.Text);
        if (path is not null)
        {
            context.Launcher.RevealInFileManager(path);
        }

        return Task.CompletedTask;
    }
}
