using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Pastes copied JSON in the other shape: indented when it is on one line, minified when it spans several.
/// </summary>
public sealed class ReformatJsonAction : IClipboardAction
{
    /// <summary>The longest text, in characters, that is looked at as JSON.</summary>
    public const int MaxLength = 1048576;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>The action's stable identifier.</summary>
    public string Id => "reformat-json";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 40;

    /// <summary>Whether the text is at most <see cref="MaxLength"/> characters and, trimmed, one strict JSON object or array.</summary>
    /// <param name="text">The text to look at, or null.</param>
    /// <returns>True when the text can be reformatted.</returns>
    public static bool Parse(string? text)
    {
        if (text is null || text.Length > MaxLength)
        {
            return false;
        }

        try
        {
            Rewrite(text, false);
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

    /// <summary>Writes the JSON with every member and element on its own line, indented by two spaces per level.</summary>
    /// <param name="text">The JSON to format.</param>
    /// <returns>The formatted JSON.</returns>
    public static string Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Rewrite(text, true);
    }

    /// <summary>Writes the JSON with no white space between its values.</summary>
    /// <param name="text">The JSON to minify.</param>
    /// <returns>The minified JSON.</returns>
    public static string Minify(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Rewrite(text, false);
    }

    /// <summary>The action's title, naming the shape it pastes.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item) ? Strings.ActionFormatJson : Strings.ActionMinifyJson;
    }

    /// <summary>The action's description, following its title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item) ? Strings.ActionFormatJsonDescription : Strings.ActionMinifyJsonDescription;
    }

    /// <summary>Whether the item is text holding a JSON object or array.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action is offered.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Parse(item.Snapshot.Text);
    }

    /// <summary>Pastes the JSON in the other shape, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paste is done.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var text = item.Snapshot.Text ?? string.Empty;
        var result = IsOneLine(item) ? Format(text) : Minify(text);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = result }, cancellationToken);
    }

    private static bool IsOneLine(ClipboardItem item)
    {
        var trimmed = (item.Snapshot.Text ?? string.Empty).Trim();
        return !trimmed.Contains('\n') && !trimmed.Contains('\r');
    }

    private static string Rewrite(string text, bool indented)
    {
        var bytes = StrictUtf8.GetBytes(text.Trim());
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = int.MaxValue });
        if (!reader.Read() || reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
        {
            throw new JsonException("Only a JSON object or array can be reformatted.");
        }

        var output = new StringBuilder();
        var depth = 0;
        var justOpened = false;
        var afterName = false;
        do
        {
            var token = reader.TokenType;
            if (token is JsonTokenType.EndObject or JsonTokenType.EndArray)
            {
                depth--;
                if (!justOpened)
                {
                    NewLine(output, indented, depth);
                }

                output.Append(token == JsonTokenType.EndObject ? '}' : ']');
                justOpened = false;
                continue;
            }

            if (afterName)
            {
                afterName = false;
            }
            else if (justOpened)
            {
                NewLine(output, indented, depth);
            }
            else if (depth > 0)
            {
                output.Append(',');
                NewLine(output, indented, depth);
            }

            justOpened = false;
            switch (token)
            {
                case JsonTokenType.StartObject:
                    output.Append('{');
                    depth++;
                    justOpened = true;
                    break;
                case JsonTokenType.StartArray:
                    output.Append('[');
                    depth++;
                    justOpened = true;
                    break;
                case JsonTokenType.PropertyName:
                    AppendString(output, reader.GetString() ?? string.Empty);
                    output.Append(indented ? ": " : ":");
                    afterName = true;
                    break;
                case JsonTokenType.String:
                    AppendString(output, reader.GetString() ?? string.Empty);
                    break;
                case JsonTokenType.Number:
                    output.Append(Encoding.UTF8.GetString(reader.ValueSpan));
                    break;
                case JsonTokenType.True:
                    output.Append("true");
                    break;
                case JsonTokenType.False:
                    output.Append("false");
                    break;
                case JsonTokenType.Null:
                    output.Append("null");
                    break;
                default:
                    throw new JsonException("Unexpected JSON token.");
            }
        }
        while (reader.Read());

        return output.ToString();
    }

    private static void NewLine(StringBuilder output, bool indented, int depth)
    {
        if (indented)
        {
            output.Append('\n').Append(' ', depth * 2);
        }
    }

    private static void AppendString(StringBuilder output, string value)
    {
        output.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    output.Append("\\\"");
                    break;
                case '\\':
                    output.Append("\\\\");
                    break;
                case '\n':
                    output.Append("\\n");
                    break;
                case '\r':
                    output.Append("\\r");
                    break;
                case '\t':
                    output.Append("\\t");
                    break;
                default:
                    if (c < 0x20)
                    {
                        output.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        output.Append(c);
                    }

                    break;
            }
        }

        output.Append('"');
    }
}
