using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Text;
using System.Text.Unicode;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that decodes a copied Base64 text, in the standard or the URL-safe alphabet,
/// and pastes the text it holds without adding it to the history.
/// </summary>
public sealed class DecodeBase64Action : IClipboardAction
{
    /// <summary>The longest text, in characters, that is looked at; longer texts are not decoded.</summary>
    public const int MaxLength = 1048576;

    private const int MinLength = 8;

    /// <summary>The action's unique id.</summary>
    public string Id => "decode-base64";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 42;

    /// <summary>Gets the action's title shown for the item.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64;
    }

    /// <summary>Gets the action's description shown for the item.</summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64Description;
    }

    /// <summary>Tells whether the item is a text that decodes from Base64 to readable text.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the text the item's Base64 holds; nothing is added to the history.</summary>
    /// <param name="item">The item to decode.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the decoded text was pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var decoded = TryDecode(item.Snapshot.Text);
        if (decoded is null)
        {
            return Task.CompletedTask;
        }

        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = decoded }, cancellationToken);
    }

    /// <summary>
    /// Decodes a Base64 text, standard or URL-safe, with or without padding, into readable UTF-8 text.
    /// Never throws.
    /// </summary>
    /// <param name="text">The text to decode, or null.</param>
    /// <returns>The decoded text, or null when the text is not Base64 of readable text.</returns>
    public static string? TryDecode(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var compact = text.Trim()
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
        if (compact.Length < MinLength || compact.Length > MaxLength)
        {
            return null;
        }

        var padding = 0;
        while (padding < compact.Length && compact[compact.Length - 1 - padding] == '=')
        {
            padding++;
        }

        if (padding > 2)
        {
            return null;
        }

        var bodyLength = compact.Length - padding;
        int totalLength;
        if (padding == 0)
        {
            var remainder = bodyLength % 4;
            if (remainder == 1)
            {
                return null;
            }

            totalLength = remainder == 0 ? bodyLength : bodyLength + (4 - remainder);
        }
        else
        {
            if (compact.Length % 4 != 0)
            {
                return null;
            }

            totalLength = compact.Length;
        }

        var chars = new char[totalLength];
        for (var i = 0; i < bodyLength; i++)
        {
            var c = compact[i];
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/')
            {
                chars[i] = c;
            }
            else if (c == '-')
            {
                chars[i] = '+';
            }
            else if (c == '_')
            {
                chars[i] = '/';
            }
            else
            {
                return null;
            }
        }

        for (var i = bodyLength; i < totalLength; i++)
        {
            chars[i] = '=';
        }

        var bytes = new byte[totalLength / 4 * 3];
        if (!Convert.TryFromBase64Chars(chars, bytes, out var written))
        {
            return null;
        }

        var decodedBytes = bytes.AsSpan(0, written);
        if (!Utf8.IsValid(decodedBytes))
        {
            return null;
        }

        var decoded = Encoding.UTF8.GetString(decodedBytes);
        foreach (var c in decoded)
        {
            if (char.IsControl(c) && c is not ('\t' or '\n' or '\r'))
            {
                return null;
            }
        }

        return decoded;
    }
}
