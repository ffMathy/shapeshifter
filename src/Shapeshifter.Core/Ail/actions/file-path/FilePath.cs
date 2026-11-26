using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Turns copied text into the absolute local path of a file or folder that exists, so the file itself can be copied.
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
        if (candidate.Length > MaxLength)
        {
            return null;
        }

        if (candidate.Length >= 2
            && (candidate[0] == '"' || candidate[0] == '\'')
            && candidate[^1] == candidate[0])
        {
            candidate = candidate[1..^1];
        }

        if (candidate.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            var rest = candidate[FileScheme.Length..];
            if (!rest.StartsWith('/'))
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
        else if (candidate == "~" || candidate.StartsWith("~/", StringComparison.Ordinal) || candidate.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidate = candidate.Length <= 2 ? home : Path.Join(home, candidate[2..]);
        }

        if (candidate.Contains('\n') || candidate.Contains('\r'))
        {
            return null;
        }

        if (candidate.StartsWith(@"\\", StringComparison.Ordinal) || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        if (!IsAbsolute(candidate))
        {
            return null;
        }

        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return null;
        }

        return Path.GetFullPath(candidate);
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
/// The action that copies the file or folder a text item's path names, ready to paste in a file manager or an e-mail.
/// </summary>
public sealed class CopyFileAtPathAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "copy-file-at-path";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 110;

    /// <summary>Gives the action's title as the user sees it.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPath;
    }

    /// <summary>Gives the action's description as the user sees it.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionCopyFileAtPathDescription;
    }

    /// <summary>Tells whether the item is text naming an existing local file or folder.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action can copy the file at the item's path.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Puts the file or folder at the item's path on the clipboard and adds it to the history, without pasting.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes when the file is on the clipboard and in the history.</returns>
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
