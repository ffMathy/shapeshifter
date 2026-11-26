using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Recognises copied text that is the absolute path of an existing local file or folder.
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
            && ((candidate[0] == '"' && candidate[^1] == '"')
                || (candidate[0] == '\'' && candidate[^1] == '\'')))
        {
            candidate = candidate[1..^1];
        }

        if (candidate.Length > MaxLength
            || candidate.Contains('\n', StringComparison.Ordinal)
            || candidate.Contains('\r', StringComparison.Ordinal))
        {
            return null;
        }

        if (candidate.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            var rest = candidate[FileScheme.Length..];
            if (rest.Length == 0 || rest[0] != '/')
            {
                return null;
            }

            candidate = Uri.UnescapeDataString(rest);

            if (OperatingSystem.IsWindows()
                && candidate.Length >= 3
                && candidate[0] == '/'
                && char.IsAsciiLetter(candidate[1])
                && candidate[2] == ':')
            {
                candidate = candidate[1..];
            }
        }
        else if (candidate == "~")
        {
            candidate = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (candidate.StartsWith("~/", StringComparison.Ordinal)
            || candidate.StartsWith("~\\", StringComparison.Ordinal))
        {
            candidate = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                candidate[2..]);
        }

        if (candidate.StartsWith("\\\\", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        if (!IsAbsolute(candidate) || candidate.Contains('\0', StringComparison.Ordinal))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(candidate);

        return File.Exists(fullPath) || Directory.Exists(fullPath) ? fullPath : null;
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

        return path.Length >= 1 && path[0] == '/';
    }
}

/// <summary>
/// Puts the file or folder a copied path names on the clipboard, and in the history, as a file.
/// </summary>
public sealed class CopyFileAtPathAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "copy-file-at-path";

    /// <summary>Where the action stands among the actions.</summary>
    public int Order => 110;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPath;
    }

    /// <summary>Gives the action's description.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPathDescription;
    }

    /// <summary>Tells whether the item is text naming an existing local file or folder.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Puts the file or folder at the item's path on the clipboard and in the history; nothing is pasted.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes when the file is on the clipboard.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var path = FilePath.Resolve(item.Snapshot.Text);
        if (path is null)
        {
            return;
        }

        await context.CopyNewContentAsync(new ClipboardSnapshot { Files = [path] }, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Opens the folder that holds the file or folder a copied path names, with it selected.
/// </summary>
public sealed class ShowPathInFileManagerAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "show-path-in-file-manager";

    /// <summary>Where the action stands among the actions.</summary>
    public int Order => 230;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionShowInFileManager;
    }

    /// <summary>Gives the action's description.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionShowPathInFileManagerDescription;
    }

    /// <summary>Tells whether the item is text naming an existing local file or folder.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Reveals the item's path in the system's file manager; the clipboard and the history are unchanged.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
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
