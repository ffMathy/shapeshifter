using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that starts a new email to the address the item holds, in the user's chosen mail app.
/// </summary>
public sealed class WriteEmailAction : IClipboardAction
{
    private const string MailtoPrefix = "mailto:";

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "write-email";

    /// <summary>
    /// Where the action is placed among the actions.
    /// </summary>
    public int Order => 210;

    /// <summary>
    /// Gets the action's title shown in the actions.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionWriteEmail;
    }

    /// <summary>
    /// Gets the action's description, naming the address the email will be written to.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var address = ParseAddress(item.Snapshot.Text);
        return address is null
            ? Strings.ActionWriteEmailDescription
            : Localizer.Format(Strings.ActionWriteEmailDescription, address);
    }

    /// <summary>
    /// Tells whether the item is text holding a single email address on its own.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action can write an email to the item's address.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ParseAddress(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Opens a mailto link to the item's address with the system's default mail app; pastes nothing.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context giving access to the launcher.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes once the link has been handed to the system.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var address = ParseAddress(item.Snapshot.Text);
        if (address is not null)
        {
            context.Launcher.OpenUrl(new Uri(MailtoPrefix + address));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads a single email address from text that holds it and nothing else, optionally as a mailto link.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The address as written, or null when the text is not exactly one address.</returns>
    public static string? ParseAddress(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var candidate = text.Trim();
        if (candidate.StartsWith(MailtoPrefix, StringComparison.OrdinalIgnoreCase))
        {
            candidate = candidate[MailtoPrefix.Length..];
        }

        if (candidate.Length == 0 || candidate.Length > 254)
        {
            return null;
        }

        var at = candidate.IndexOf('@', StringComparison.Ordinal);
        if (at < 0 || candidate.IndexOf('@', at + 1) >= 0)
        {
            return null;
        }

        var local = candidate[..at];
        var domain = candidate[(at + 1)..];
        return IsLocalPart(local) && IsDomain(domain) ? candidate : null;
    }

    private static bool IsLocalPart(string local)
    {
        if (local.Length < 1 || local.Length > 64)
        {
            return false;
        }

        if (local[0] == '.' || local[^1] == '.' || local.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var rune in local.EnumerateRunes())
        {
            if (!IsLetterOrDigit(rune) && !IsLocalSymbol(rune))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLocalSymbol(Rune rune) =>
        rune.Value is '.' or '_' or '%' or '+' or '-' or '\'';

    private static bool IsDomain(string domain)
    {
        var labels = domain.Split('.');
        if (labels.Length < 2)
        {
            return false;
        }

        foreach (var label in labels)
        {
            if (label.Length == 0)
            {
                return false;
            }

            foreach (var rune in label.EnumerateRunes())
            {
                if (!IsLetterOrDigit(rune) && rune.Value != '-')
                {
                    return false;
                }
            }
        }

        var last = labels[^1];
        var letters = 0;
        foreach (var rune in last.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                return false;
            }

            letters++;
        }

        return letters >= 2;
    }

    private static bool IsLetterOrDigit(Rune rune) =>
        Rune.IsLetter(rune) || (rune.IsAscii && char.IsAsciiDigit((char)rune.Value));
}
