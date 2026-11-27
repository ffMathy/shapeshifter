using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes a text with its %-codes turned back into the characters they stand for.
/// </summary>
public sealed class UrlDecodeAction : IClipboardAction
{
    /// <summary>
    /// The length above which a text is not looked at.
    /// </summary>
    public const int MaxLength = 1048576;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "url-decode";

    /// <summary>
    /// Where the action is placed among the actions.
    /// </summary>
    public int Order => 44;

    /// <summary>
    /// Gives the action's title for the item.
    /// </summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUrlDecode;
    }

    /// <summary>
    /// Gives the action's description for the item.
    /// </summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUrlDecodeDescription;
    }

    /// <summary>
    /// Tells whether the item is text that holds a %-code and decodes to readable UTF-8.
    /// </summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the item's text decoded, without adding it to the history.
    /// </summary>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var decoded = TryDecode(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item's text cannot be URL-decoded.");
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = decoded }, cancellationToken);
    }

    /// <summary>
    /// Decodes every run of %-codes in the text as UTF-8, or gives null when there is nothing readable to decode.
    /// </summary>
    public static string? TryDecode(string? text)
    {
        if (text is null || text.Length > MaxLength)
        {
            return null;
        }

        var result = new StringBuilder(text.Length);
        var bytes = new List<byte>();
        var found = false;
        var i = 0;
        while (i < text.Length)
        {
            if (IsCode(text, i))
            {
                bytes.Clear();
                while (IsCode(text, i))
                {
                    bytes.Add(byte.Parse(text.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                    i += 3;
                }

                try
                {
                    result.Append(StrictUtf8.GetString(bytes.ToArray()));
                }
                catch (DecoderFallbackException)
                {
                    return null;
                }

                found = true;
            }
            else
            {
                result.Append(text[i]);
                i++;
            }
        }

        if (!found)
        {
            return null;
        }

        var decoded = result.ToString();
        foreach (var c in decoded)
        {
            if (char.IsControl(c) && c is not '\t' and not '\n' and not '\r')
            {
                return null;
            }
        }

        return decoded;
    }

    private static bool IsCode(string text, int index) =>
        index + 2 < text.Length
        && text[index] == '%'
        && char.IsAsciiHexDigit(text[index + 1])
        && char.IsAsciiHexDigit(text[index + 2]);
}
