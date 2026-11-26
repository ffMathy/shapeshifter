using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes copied JSON in the other shape: indented when it is on one line, minified when it spans several.
/// </summary>
public sealed class ReformatJsonAction : IClipboardAction
{
    /// <summary>The longest text, in characters, that is looked at; longer texts are not JSON to this action.</summary>
    public const int MaxLength = 1048576;

    private static readonly JsonReaderOptions ReaderOptions = new()
    {
        CommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        MaxDepth = MaxLength,
    };

    /// <summary>The action's unique id.</summary>
    public string Id => "reformat-json";

    /// <summary>The action's place among the actions.</summary>
    public int Order => 40;

    /// <summary>Gets the title: format for JSON on one line, minify for JSON over several lines.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item.Snapshot.Text) ? Strings.ActionFormatJson : Strings.ActionMinifyJson;
    }

    /// <summary>Gets the description, following the title.</summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return IsOneLine(item.Snapshot.Text) ? Strings.ActionFormatJsonDescription : Strings.ActionMinifyJsonDescription;
    }

    /// <summary>Tells whether the item is text holding one strict JSON object or array.</summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can reshape the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Parse(item.Snapshot.Text);
    }

    /// <summary>Pastes the item's JSON reshaped, without adding it to the history.</summary>
    /// <param name="item">The item to paste.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paste is done.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var text = item.Snapshot.Text ?? string.Empty;
        var result = IsOneLine(text) ? Format(text) : Minify(text);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = result }, cancellationToken);
    }

    /// <summary>Tells whether the text is at most MaxLength characters and, trimmed, is one strict JSON object or array.</summary>
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
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text.Trim()), ReaderOptions);
            if (!reader.Read() || reader.TokenType is not (JsonTokenType.StartObject or JsonTokenType.StartArray))
            {
                return false;
            }

            while (reader.Read())
            {
                if (reader.TokenType is JsonTokenType.String or JsonTokenType.PropertyName)
                {
                    _ = reader.GetString();
                }
            }

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
    }

    /// <summary>Writes the JSON indented by two spaces per level, one member or element per line.</summary>
    /// <param name="text">The JSON to format.</param>
    /// <returns>The formatted JSON.</returns>
    public static string Format(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Write(text, indented: true);
    }

    /// <summary>Writes the JSON with no white space between its values.</summary>
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
        var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(text.Trim()), ReaderOptions);
        var output = new StringBuilder();
        var hasChildren = new Stack<bool>();
        var afterName = false;

        void BeginValue()
        {
            if (afterName)
            {
                afterName = false;
                return;
            }

            if (hasChildren.Count == 0)
            {
                return;
            }

            if (hasChildren.Peek())
            {
                output.Append(',');
            }
            else
            {
                hasChildren.Pop();
                hasChildren.Push(true);
            }

            if (indented)
            {
                output.Append('\n').Append(' ', hasChildren.Count * 2);
            }
        }

        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    BeginValue();
                    output.Append(reader.TokenType == JsonTokenType.StartObject ? '{' : '[');
                    hasChildren.Push(false);
                    break;
                case JsonTokenType.EndObject:
                case JsonTokenType.EndArray:
                    if (hasChildren.Pop() && indented)
                    {
                        output.Append('\n').Append(' ', hasChildren.Count * 2);
                    }

                    output.Append(reader.TokenType == JsonTokenType.EndObject ? '}' : ']');
                    break;
                case JsonTokenType.PropertyName:
                    BeginValue();
                    AppendString(output, reader.GetString()!);
                    output.Append(indented ? ": " : ":");
                    afterName = true;
                    break;
                case JsonTokenType.String:
                    BeginValue();
                    AppendString(output, reader.GetString()!);
                    break;
                case JsonTokenType.Number:
                    BeginValue();
                    output.Append(Encoding.UTF8.GetString(reader.ValueSpan));
                    break;
                case JsonTokenType.True:
                    BeginValue();
                    output.Append("true");
                    break;
                case JsonTokenType.False:
                    BeginValue();
                    output.Append("false");
                    break;
                case JsonTokenType.Null:
                    BeginValue();
                    output.Append("null");
                    break;
                default:
                    break;
            }
        }

        return output.ToString();
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
                    if (c < ' ')
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
