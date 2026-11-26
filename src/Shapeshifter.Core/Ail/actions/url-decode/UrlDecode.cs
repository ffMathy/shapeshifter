using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
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
    /// The unique id of this action.
    /// </summary>
    public string Id => "url-decode";

    /// <summary>
    /// The position of this action among the actions.
    /// </summary>
    public int Order => 44;

    /// <summary>
    /// Gives the title shown for this action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUrlDecode;
    }

    /// <summary>
    /// Gives the description shown for this action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionUrlDecodeDescription;
    }

    /// <summary>
    /// Tells whether the item is a text that holds %-codes decoding to readable UTF-8.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && TryDecode(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the item's text with its %-codes decoded, without adding it to the history.
    /// </summary>
    /// <param name="item">The item to decode and paste.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
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
    /// Decodes every run of %-codes in a text as UTF-8, leaving every other character as it is.
    /// </summary>
    /// <param name="text">The text to decode.</param>
    /// <returns>The decoded text, or null when there is nothing to decode or the result would not be readable.</returns>
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
            if (!IsCodeAt(text, i))
            {
                builder.Append(text[i]);
                i++;
                continue;
            }

            found = true;
            bytes.Clear();
            while (IsCodeAt(text, i))
            {
                bytes.Add(byte.Parse(text.AsSpan(i + 1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture));
                i += 3;
            }

            var run = bytes.ToArray();
            if (!Utf8.IsValid(run))
            {
                return null;
            }

            builder.Append(Encoding.UTF8.GetString(run));
        }

        if (!found)
        {
            return null;
        }

        var result = builder.ToString();
        foreach (var c in result)
        {
            if (char.IsControl(c) && c != '\t' && c != '\n' && c != '\r')
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
}
