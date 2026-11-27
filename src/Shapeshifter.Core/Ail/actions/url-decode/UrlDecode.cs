using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that pastes a text with its %-codes turned back into the characters they stand for.
/// </summary>
public sealed class UrlDecodeAction : IClipboardAction
{
    /// <summary>
    /// The length above which a text is not looked at.
    /// </summary>
    public const int MaxLength = 1048576;

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "url-decode";

    /// <summary>
    /// Where the action stands among the actions.
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
    /// Tells whether the item is a text that holds %-codes decoding to readable UTF-8.
    /// </summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the item's text URL-decoded, without adding it to the history.
    /// </summary>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var decoded = TryDecode(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item's text has nothing to URL-decode.");
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = decoded }, cancellationToken);
    }

    /// <summary>
    /// Decodes the %-codes in a text, or gives null when there is nothing readable to decode.
    /// </summary>
    public static string? TryDecode(string? text)
    {
        if (text is null || text.Length > MaxLength)
        {
            return null;
        }

        var builder = new StringBuilder(text.Length);
        var bytes = new List<byte>();
        var found = false;
        var i = 0;
        while (i < text.Length)
        {
            if (IsCodeAt(text, i))
            {
                bytes.Clear();
                while (IsCodeAt(text, i))
                {
                    bytes.Add((byte)((HexValue(text[i + 1]) << 4) | HexValue(text[i + 2])));
                    i += 3;
                }

                var span = CollectionsMarshal.AsSpan(bytes);
                if (!Utf8.IsValid(span))
                {
                    return null;
                }

                builder.Append(Encoding.UTF8.GetString(span));
                found = true;
            }
            else
            {
                builder.Append(text[i]);
                i++;
            }
        }

        if (!found)
        {
            return null;
        }

        var result = builder.ToString();
        foreach (var c in result)
        {
            if (char.IsControl(c) && c is not ('\t' or '\n' or '\r'))
            {
                return null;
            }
        }

        return result;
    }

    private static bool IsCodeAt(string text, int index) =>
        index + 2 < text.Length
        && text[index] == '%'
        && char.IsAsciiHexDigit(text[index + 1])
        && char.IsAsciiHexDigit(text[index + 2]);

    private static int HexValue(char c) =>
        c <= '9' ? c - '0' : (c | 0x20) - 'a' + 10;
}
