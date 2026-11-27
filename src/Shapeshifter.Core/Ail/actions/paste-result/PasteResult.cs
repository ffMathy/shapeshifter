using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that calculates a copied sum, such as "12*7+3", and pastes its result instead of the text.
/// </summary>
public sealed partial class PasteResultAction : IClipboardAction
{
    /// <summary>
    /// The longest text, without the white space at its start and end, that is read as a calculation.
    /// </summary>
    public const int MaxLength = 200;

    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-result";

    /// <summary>
    /// Where the action is placed among the actions.
    /// </summary>
    public int Order => 52;

    /// <summary>
    /// Gets the title shown for the action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The action's title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteResult;
    }

    /// <summary>
    /// Gets the description shown for the action, which tells the result that will be pasted.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The action's description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string? result = Calculate(item.Snapshot.Text);
        return result is null
            ? Strings.ActionPasteResultDescription
            : Localizer.Format(Strings.ActionPasteResultDescription, result);
    }

    /// <summary>
    /// Tells whether the item is a text that can be calculated.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is text with a result.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Calculate(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the item's result without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action may use.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes once the result is pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Calculate(item.Snapshot.Text) }, cancellationToken);
    }

    /// <summary>
    /// Calculates a text that holds only numbers, + - * / % ^ × ÷ and parentheses.
    /// </summary>
    /// <param name="text">The text to calculate, or null.</param>
    /// <returns>The result written with the invariant culture, or null when the text is not a calculation.</returns>
    public static string? Calculate(string? text)
    {
        if (text is null)
        {
            return null;
        }

        string trimmed = text.Trim();
        if (trimmed.Length > MaxLength)
        {
            return null;
        }

        foreach (char c in trimmed)
        {
            if (!char.IsAsciiDigit(c) && c != ' ' && c != '.' && c != '(' && c != ')' && !IsOperator(c))
            {
                return null;
            }
        }

        if (!HoldsBinaryOperator(trimmed))
        {
            return null;
        }

        if (JoinedDigitGroups().IsMatch(trimmed))
        {
            return null;
        }

        string expression = trimmed
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

        string? result = value switch
        {
            int number => number.ToString(CultureInfo.InvariantCulture),
            long number => number.ToString(CultureInfo.InvariantCulture),
            double number when double.IsFinite(number) =>
                Math.Round(number, 10, MidpointRounding.AwayFromZero).ToString("0.##########", CultureInfo.InvariantCulture),
            _ => null,
        };

        return result is null || string.Equals(result, trimmed, StringComparison.Ordinal) ? null : result;
    }

    private static bool IsOperator(char c) =>
        c is '+' or '-' or '*' or '/' or '%' or '^' or '×' or '÷';

    private static bool HoldsBinaryOperator(string text)
    {
        char previous = '\0';
        foreach (char c in text)
        {
            if (c == ' ')
            {
                continue;
            }

            if (IsOperator(c) && (char.IsAsciiDigit(previous) || previous == '.' || previous == ')'))
            {
                return true;
            }

            previous = c;
        }

        return false;
    }

    [GeneratedRegex(@"^[0-9]+([-/.])[0-9]+(?:\1[0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex JoinedDigitGroups();
}
