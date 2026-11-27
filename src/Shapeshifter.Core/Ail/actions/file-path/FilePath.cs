using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Works out whether copied text names a file or folder on this computer.
/// </summary>
public static class FilePath
{
    private const int MaxLength = 1024;
    private const string FileScheme = "file://";

    /// <summary>
    /// Gives the full path of the existing local file or folder the text names, or null when it names none.
    /// </summary>
    /// <param name="text">The copied text, or null.</param>
    /// <returns>The full path, as the system writes it, or null.</returns>
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

        if (candidate.StartsWith(FileScheme, StringComparison.OrdinalIgnoreCase))
        {
            var rest = candidate[FileScheme.Length..];
            if (!rest.StartsWith('/'))
            {
                return null;
            }

            candidate = Uri.UnescapeDataString(rest);
            if (OperatingSystem.IsWindows() && candidate.Length >= 3 && IsDrive(candidate, 1))
            {
                candidate = candidate[1..];
            }
        }

        if (candidate == "~")
        {
            candidate = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        else if (candidate.StartsWith("~/", StringComparison.Ordinal)
            || candidate.StartsWith("~\\", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidate = home + Path.DirectorySeparatorChar + candidate[2..];
        }

        if (candidate.StartsWith("\\\\", StringComparison.Ordinal)
            || candidate.StartsWith("//", StringComparison.Ordinal))
        {
            return null;
        }

        var isAbsolute = OperatingSystem.IsWindows()
            ? candidate.Length >= 3 && IsDrive(candidate, 0)
            : candidate.StartsWith('/');
        if (!isAbsolute)
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(candidate);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        return File.Exists(fullPath) || Directory.Exists(fullPath) ? fullPath : null;
    }

    private static bool IsDrive(string value, int start) =>
        value.Length >= start + 3
        && char.IsAsciiLetter(value[start])
        && value[start + 1] == ':'
        && (value[start + 2] == '\\' || value[start + 2] == '/');
}

/// <summary>
/// Copies the file or folder a copied path names, so it can be pasted in a file manager or an email.
/// </summary>
public sealed class CopyFileAtPathAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "copy-file-at-path";

    /// <summary>Where the action is placed among the actions.</summary>
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
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Puts the named file or folder on the clipboard and adds it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A task that completes once the file is copied.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var path = FilePath.Resolve(item.Snapshot.Text) ?? throw new InvalidOperationException();
        return context.CopyNewContentAsync(new ClipboardSnapshot { Files = [path] }, cancellationToken);
    }
}

/// <summary>
/// Opens the folder holding the file or folder a copied path names, with it selected.
/// </summary>
public sealed class ShowPathInFileManagerAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "show-path-in-file-manager";

    /// <summary>Where the action is placed among the actions.</summary>
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
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && FilePath.Resolve(item.Snapshot.Text) is not null;
    }

    /// <summary>Reveals the named file or folder in the system's file manager.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Not used; revealing does not wait.</param>
    /// <returns>A completed task.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var path = FilePath.Resolve(item.Snapshot.Text) ?? throw new InvalidOperationException();
        context.Launcher.RevealInFileManager(path);
        return Task.CompletedTask;
    }
}
