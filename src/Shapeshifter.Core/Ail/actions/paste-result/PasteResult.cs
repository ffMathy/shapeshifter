using System.Globalization;
using System.Text.RegularExpressions;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that calculates a copied sum, such as 12*7+3, and pastes its result.
/// </summary>
public sealed partial class PasteResultAction : IClipboardAction
{
    /// <summary>
    /// The longest text, without the white space at its start and end, that is read as a calculation.
    /// </summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "paste-result";

    /// <summary>
    /// The position of this action among the actions.
    /// </summary>
    public int Order => 52;

    /// <summary>
    /// Gets the title shown for this action.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteResult;
    }

    /// <summary>
    /// Gets the description shown for this action, with the result that will be pasted.
    /// </summary>
    /// <param name="item">The item the action is shown for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var result = Calculate(item.Snapshot.Text);
        return result is null
            ? Strings.ActionPasteResultDescription
            : Localizer.Format(Strings.ActionPasteResultDescription, result);
    }

    /// <summary>
    /// Tells whether the item is a text that can be calculated.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item is a text with a result.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Calculate(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the result of the item's calculation without adding it to the history.
    /// </summary>
    /// <param name="item">The item to calculate.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the result is pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var result = Calculate(item.Snapshot.Text);
        if (result is null)
        {
            return;
        }

        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = result }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Calculates a text made of numbers, operators and parentheses.
    /// </summary>
    /// <param name="text">The text to calculate.</param>
    /// <returns>The result written with the invariant culture, or null when the text is not a calculation.</returns>
    public static string? Calculate(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return null;
        }

        var hasBinaryOperator = false;
        var last = '\0';
        foreach (var c in trimmed)
        {
            if (c == ' ')
            {
                continue;
            }

            if (IsOperator(c))
            {
                if (char.IsAsciiDigit(last) || last == '.' || last == ')')
                {
                    hasBinaryOperator = true;
                }
            }
            else if (!char.IsAsciiDigit(c) && c != '.' && c != '(' && c != ')')
            {
                return null;
            }

            last = c;
        }

        if (!hasBinaryOperator || JoinedDigitGroups().IsMatch(trimmed))
        {
            return null;
        }

        var expression = trimmed
            .Replace('×', '*')
            .Replace('÷', '/')
            .Replace("^", "**", StringComparison.Ordinal);

        object? value;
        try
        {
            value = new NCalc.Expression(
                expression,
                NCalc.ExpressionConfiguration.FromOptions(NCalc.ExpressionOptions.OverflowProtection),
                null,
                CultureInfo.InvariantCulture,
                null).Evaluate();
        }
        catch (Exception)
        {
            return null;
        }

        string? result;
        switch (value)
        {
            case int i:
                result = i.ToString(CultureInfo.InvariantCulture);
                break;
            case long l:
                result = l.ToString(CultureInfo.InvariantCulture);
                break;
            case double d:
                if (!double.IsFinite(d))
                {
                    return null;
                }

                var rounded = Math.Round(d, 10, MidpointRounding.AwayFromZero);
                if (rounded == 0)
                {
                    rounded = 0;
                }

                result = rounded.ToString("F10", CultureInfo.InvariantCulture).TrimEnd('0').TrimEnd('.');
                break;
            default:
                return null;
        }

        return string.Equals(result, trimmed, StringComparison.Ordinal) ? null : result;
    }

    private static bool IsOperator(char c) =>
        c is '+' or '-' or '*' or '/' or '%' or '^' or '×' or '÷';

    [GeneratedRegex(@"^\d+([-/.])\d+(?:\1\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex JoinedDigitGroups();
}
