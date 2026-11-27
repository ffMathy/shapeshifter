using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Text;
using System.Text.Json;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes copied JSON in the other shape: indented when it is on one line, minified when it spans several.
/// </summary>
public sealed class ReformatJsonAction : IClipboardAction
{
    /// <summary>
    /// The longest text, in characters, that is looked at; longer texts are never offered this action.
    /// </summary>
    public const int MaxLength = 1048576;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly char[] HexDigits = "0123456789ABCDEF".ToCharArray();

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "reformat-json";

    /// <summary>
    /// The action's place among the actions.
    /// </summary>
    public int Order => 40;

    /// <summary>
    /// Gets the title shown for the item: format for JSON on one line, minify for JSON over several lines.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item.Snapshot.Text) ? Strings.ActionFormatJson : Strings.ActionMinifyJson;
    }

    /// <summary>
    /// Gets the description shown for the item, following its title.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item.Snapshot.Text) ? Strings.ActionFormatJsonDescription : Strings.ActionMinifyJsonDescription;
    }

    /// <summary>
    /// Tells whether the item is text holding one strict JSON object or array.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can reshape the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Parse(item.Snapshot.Text);
    }

    /// <summary>
    /// Pastes the item's JSON in the other shape, without adding it to the history.
    /// </summary>
    /// <param name="item">The item to reshape.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paste was made.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var text = item.Snapshot.Text ?? string.Empty;
        var result = IsOneLine(text) ? Format(text) : Minify(text);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = result }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Tells whether the text is at most MaxLength characters long and, without the white space around it, is one strict JSON object or array.
    /// </summary>
    /// <param name="text">The text to check, or null.</param>
    /// <returns>True when the text parses.</returns>
    public static bool Parse(string? text)
    {
        if (text is null || text.Length > MaxLength)
        {
            return false;
        }

        try
        {
            Write(text, indented: false);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the JSON with every member and element on its own line, indented by two spaces per level.
    /// </summary>
    /// <param name="text">The JSON to format.</param>
    /// <returns>The indented JSON.</returns>
    public static string Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Write(text, indented: true);
    }

    /// <summary>
    /// Writes the JSON with no white space between its values.
    /// </summary>
    /// <param name="text">The JSON to minify.</param>
    /// <returns>The minified JSON.</returns>
    public static string Minify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Write(text, indented: false);
    }

    private static bool IsOneLine(string? text)
    {
        var trimmed = (text ?? string.Empty).Trim();
        return trimmed.IndexOf('\n', StringComparison.Ordinal) < 0 && trimmed.IndexOf('\r', StringComparison.Ordinal) < 0;
    }

    private static string Write(string text, bool indented)
    {
        var reader = new Utf8JsonReader(StrictUtf8.GetBytes(text.Trim()), new JsonReaderOptions { MaxDepth = int.MaxValue });
        if (!reader.Read() || (reader.TokenType != JsonTokenType.StartObject && reader.TokenType != JsonTokenType.StartArray))
        {
            throw new JsonException("Only an object or an array can be reshaped.");
        }

        var builder = new StringBuilder();
        var depth = 0;
        var first = true;
        var afterName = false;
        do
        {
            var token = reader.TokenType;
            if (token is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
                if (!first && indented)
                {
                    AppendNewLine(builder, depth);
                }

                builder.Append(token == JsonTokenType.EndObject ? '}' : ']');
                first = false;
                continue;
            }

            if (afterName)
            {
                afterName = false;
            }
            else if (depth > 0)
            {
                if (!first)
                {
                    builder.Append(',');
                }

                if (indented)
                {
                    AppendNewLine(builder, depth);
                }
            }

            switch (token)
            {
                case JsonTokenType.StartObject:
                    builder.Append('{');
                    depth++;
                    first = true;
                    break;
                case JsonTokenType.StartArray:
                    builder.Append('[');
                    depth++;
                    first = true;
                    break;
                case JsonTokenType.PropertyName:
                    AppendString(builder, reader.GetString() ?? string.Empty);
                    builder.Append(indented ? ": " : ":");
                    afterName = true;
                    first = false;
                    break;
                case JsonTokenType.String:
                    AppendString(builder, reader.GetString() ?? string.Empty);
                    first = false;
                    break;
                case JsonTokenType.Number:
                    builder.Append(Encoding.UTF8.GetString(reader.ValueSpan));
                    first = false;
                    break;
                case JsonTokenType.True:
                    builder.Append("true");
                    first = false;
                    break;
                case JsonTokenType.False:
                    builder.Append("false");
                    first = false;
                    break;
                case JsonTokenType.Null:
                    builder.Append("null");
                    first = false;
                    break;
                default:
                    throw new JsonException("Unexpected JSON token.");
            }
        }
        while (reader.Read());

        return builder.ToString();
    }

    private static void AppendNewLine(StringBuilder builder, int depth)
    {
        builder.Append('\n').Append(' ', depth * 2);
    }

    private static void AppendString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (c < '\u0020')
                    {
                        builder.Append("\\u00").Append(HexDigits[c >> 4]).Append(HexDigits[c & 0xF]);
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }

        builder.Append('"');
    }
}
