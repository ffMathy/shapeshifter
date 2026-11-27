using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Text;
using System.Text.Unicode;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that decodes a copied Base64 text and pastes the readable text it holds,
/// without adding it to the history.
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

    /// <summary>Gives the action's title shown for the item.</summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64;
    }

    /// <summary>Gives the action's description shown for the item.</summary>
    /// <param name="item">The item the action is offered for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64Description;
    }

    /// <summary>Tells whether the item is text that decodes from Base64 to readable text.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be used on the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the decoded text of the item, without adding it to the history.</summary>
    /// <param name="item">The item holding the Base64 text.</param>
    /// <param name="context">The context used to write the clipboard and paste.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public async System.Threading.Tasks.Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(context);
        var decoded = TryDecode(item.Snapshot.Text);
        if (decoded is null)
        {
            return;
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = decoded }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes a standard or URL-safe Base64 text, with or without padding, to the readable
    /// UTF-8 text it holds.
    /// </summary>
    /// <param name="text">The text to decode, or null.</param>
    /// <returns>The decoded text, or null when the text is not Base64 of readable text.</returns>
    public static string? TryDecode(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (c is '\r' or '\n')
            {
                continue;
            }

            builder.Append(c);
        }

        if (builder.Length < MinLength || builder.Length > MaxLength)
        {
            return null;
        }

        var padding = 0;
        while (padding < builder.Length && builder[builder.Length - 1 - padding] == '=')
        {
            padding++;
        }

        if (padding > 2)
        {
            return null;
        }

        var bodyLength = builder.Length - padding;
        var body = new StringBuilder(bodyLength + 3);
        for (var i = 0; i < bodyLength; i++)
        {
            var c = builder[i];
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/')
            {
                body.Append(c);
            }
            else if (c == '-')
            {
                body.Append('+');
            }
            else if (c == '_')
            {
                body.Append('/');
            }
            else
            {
                return null;
            }
        }

        if (bodyLength % 4 == 1)
        {
            return null;
        }

        while (body.Length % 4 != 0)
        {
            body.Append('=');
        }

        var bytes = System.Convert.FromBase64String(body.ToString());
        if (!Utf8.IsValid(bytes))
        {
            return null;
        }

        var decoded = Encoding.UTF8.GetString(bytes);
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
