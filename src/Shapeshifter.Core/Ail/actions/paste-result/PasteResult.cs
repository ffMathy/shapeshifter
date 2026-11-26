using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that calculates a copied sum, such as "12*7+3", and pastes its result.
/// </summary>
public sealed class PasteResultAction : IClipboardAction
{
    /// <summary>
    /// The greatest number of characters, without the white space at the start and end, that is read as a calculation.
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
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteResult;
    }

    /// <summary>
    /// Gets the description shown for this action, which shows the result that will be pasted.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        string? result = Calculate(item.Snapshot.Text);
        if (result is null)
        {
            return Strings.ActionPasteResultDescription;
        }

        return Localizer.Format(Strings.ActionPasteResultDescription, result);
    }

    /// <summary>
    /// Tells whether the item is text that can be calculated.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is text that gives a result.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && Calculate(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Pastes the result of the item's calculation, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the result has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        string? result = Calculate(item.Snapshot.Text);
        if (result is null)
        {
            return Task.CompletedTask;
        }

        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = result }, cancellationToken);
    }

    /// <summary>
    /// Calculates a text that holds only numbers, + - * / % ^ × ÷ and parentheses.
    /// </summary>
    /// <param name="text">The text to calculate.</param>
    /// <returns>The result, written with the invariant culture, or null when the text is not a calculation.</returns>
    public static string? Calculate(string? text)
    {
        if (text is null)
        {
            return null;
        }

        string trimmed = text.Trim();
        if (trimmed.Length > MaxLength || !HasOnlyAllowedCharacters(trimmed) || !HasBinaryOperator(trimmed))
        {
            return null;
        }

        if (IsDigitGroupsJoinedBySingleSeparator(trimmed))
        {
            return null;
        }

        string expression = trimmed
            .Replace("×", "*", StringComparison.Ordinal)
            .Replace("÷", "/", StringComparison.Ordinal)
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
            double number when double.IsFinite(number) => Math.Round(number, 10, MidpointRounding.AwayFromZero)
                .ToString("0.##########", CultureInfo.InvariantCulture),
            _ => null,
        };

        if (result is null || string.Equals(result, trimmed, StringComparison.Ordinal))
        {
            return null;
        }

        return result;
    }

    private static bool IsOperator(char c) => c is '+' or '-' or '*' or '/' or '%' or '^' or '×' or '÷';

    private static bool HasOnlyAllowedCharacters(string text)
    {
        foreach (char c in text)
        {
            if (!char.IsAsciiDigit(c) && c is not (' ' or '.' or '(' or ')') && !IsOperator(c))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasBinaryOperator(string text)
    {
        char previous = '\0';
        foreach (char c in text)
        {
            if (c == ' ')
            {
                continue;
            }

            if (IsOperator(c) && (char.IsAsciiDigit(previous) || previous is '.' or ')'))
            {
                return true;
            }

            previous = c;
        }

        return false;
    }

    private static bool IsDigitGroupsJoinedBySingleSeparator(string text)
    {
        char separator = '\0';
        bool previousWasDigit = false;
        foreach (char c in text)
        {
            if (char.IsAsciiDigit(c))
            {
                previousWasDigit = true;
                continue;
            }

            if (c is not ('-' or '/' or '.') || !previousWasDigit)
            {
                return false;
            }

            if (separator == '\0')
            {
                separator = c;
            }
            else if (c != separator)
            {
                return false;
            }

            previousWasDigit = false;
        }

        return separator != '\0' && previousWasDigit;
    }
}
