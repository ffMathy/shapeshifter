using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that decodes a copied Base64 text and pastes the text it holds,
/// without handing it to a website or a terminal first.
/// </summary>
public sealed class DecodeBase64Action : IClipboardAction
{
    /// <summary>
    /// The longest text, in characters, that is looked at; longer texts are never decoded.
    /// </summary>
    public const int MaxLength = 1048576;

    private const int MinLength = 8;

    private static readonly System.Text.UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    /// The stable id of this action.
    /// </summary>
    public string Id => "decode-base64";

    /// <summary>
    /// Where this action appears among the actions.
    /// </summary>
    public int Order => 42;

    /// <summary>
    /// Gets the title shown for this action.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64;
    }

    /// <summary>
    /// Gets the description shown for this action.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionDecodeBase64Description;
    }

    /// <summary>
    /// Tells whether the item is text that decodes from Base64 to readable text.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns><see langword="true"/> when the action can be used on the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the decoded text; nothing is added to the history.
    /// </summary>
    /// <param name="item">The item whose text is decoded.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes once the text has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var decoded = TryDecode(item.Snapshot.Text);
        if (decoded is null)
        {
            return;
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = decoded }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Decodes a standard or URL-safe Base64 text, with or without padding, to readable UTF-8 text.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <returns>The decoded text, or <see langword="null"/> when the text is not Base64 of readable text.</returns>
    public static string? TryDecode(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        var builder = new System.Text.StringBuilder(trimmed.Length);
        foreach (var c in trimmed)
        {
            if (c != '\r' && c != '\n')
            {
                builder.Append(c);
            }
        }

        var left = builder.ToString();
        if (left.Length < MinLength || left.Length > MaxLength)
        {
            return null;
        }

        var paddingGiven = 0;
        while (paddingGiven < left.Length && left[left.Length - 1 - paddingGiven] == '=')
        {
            paddingGiven++;
        }

        if (paddingGiven > 2)
        {
            return null;
        }

        var bodyLength = left.Length - paddingGiven;
        var body = new char[bodyLength];
        for (var i = 0; i < bodyLength; i++)
        {
            var c = left[i];
            if (c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '+' or '/')
            {
                body[i] = c;
            }
            else if (c == '-')
            {
                body[i] = '+';
            }
            else if (c == '_')
            {
                body[i] = '/';
            }
            else
            {
                return null;
            }
        }

        var remainder = bodyLength % 4;
        if (remainder == 1)
        {
            return null;
        }

        var paddingNeeded = (4 - remainder) % 4;
        if (paddingGiven > paddingNeeded)
        {
            return null;
        }

        var padded = new string(body) + new string('=', paddingNeeded);
        var bytes = new byte[padded.Length / 4 * 3];
        if (!Convert.TryFromBase64String(padded, bytes, out var written))
        {
            return null;
        }

        string decoded;
        try
        {
            decoded = StrictUtf8.GetString(bytes, 0, written);
        }
        catch (System.Text.DecoderFallbackException)
        {
            return null;
        }

        foreach (var c in decoded)
        {
            if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
            {
                return null;
            }
        }

        return decoded;
    }
}
