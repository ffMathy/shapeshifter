using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that starts a new email to an address that was copied on its own.
/// </summary>
public sealed class WriteEmailAction : IClipboardAction
{
    private const string MailtoPrefix = "mailto:";

    /// <summary>The unique id of this action.</summary>
    public string Id => "write-email";

    /// <summary>The position of this action among the actions.</summary>
    public int Order => 210;

    /// <summary>Returns the title shown for this action.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionWriteEmail;
    }

    /// <summary>Returns the description shown for this action, naming the address the email is to.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var address = ParseAddress(item.Snapshot.Text);
        if (address is null)
        {
            return Strings.ActionWriteEmailDescription;
        }

        return Localizer.Format(Strings.ActionWriteEmailDescription, address);
    }

    /// <summary>Tells whether the item is text holding exactly one email address.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns><see langword="true"/> when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ParseAddress(item.Snapshot.Text) is not null;
    }

    /// <summary>Opens a mailto link to the item's address with the system's default email app; pastes nothing.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context giving access to the launcher.</param>
    /// <param name="cancellationToken">A token to cancel the action.</param>
    /// <returns>A task that completes once the link has been handed to the system.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var address = ParseAddress(item.Snapshot.Text);
        if (address is not null)
        {
            context.Launcher.OpenUrl(new Uri(MailtoPrefix + address, UriKind.Absolute));
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Returns the email address the text consists of, without surrounding white space and a leading "mailto:",
    /// or <see langword="null"/> when the text is not exactly one address.
    /// </summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The address as written, or <see langword="null"/>.</returns>
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

        if (CountRunes(candidate) > 254)
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
        return IsValidLocal(local) && IsValidDomain(domain) ? candidate : null;
    }

    private static bool IsValidLocal(string local)
    {
        var length = CountRunes(local);
        if (length < 1 || length > 64)
        {
            return false;
        }

        if (local.StartsWith('.') || local.EndsWith('.') || local.Contains("..", StringComparison.Ordinal))
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

    private static bool IsValidDomain(string domain)
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
        if (CountRunes(last) < 2)
        {
            return false;
        }

        foreach (var rune in last.EnumerateRunes())
        {
            if (!Rune.IsLetter(rune))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsLetterOrDigit(Rune rune) =>
        Rune.IsLetter(rune) || (rune.IsAscii && char.IsAsciiDigit((char)rune.Value));

    private static bool IsLocalSymbol(Rune rune) =>
        rune.Value is '.' or '_' or '%' or '+' or '-' or '\'';

    private static int CountRunes(string value)
    {
        var count = 0;
        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
