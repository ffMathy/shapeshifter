using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shapeshifter.Core.Ail;

/// <summary>The notation a colour is written in.</summary>
public enum ColorNotation
{
    /// <summary>A hexadecimal colour such as #3584e4.</summary>
    Hex,

    /// <summary>An rgb() or rgba() colour.</summary>
    Rgb,

    /// <summary>An hsl() or hsla() colour.</summary>
    Hsl,
}

/// <summary>A colour read from text, with the notation it was written in.</summary>
/// <param name="Red">The red channel, 0 to 255.</param>
/// <param name="Green">The green channel, 0 to 255.</param>
/// <param name="Blue">The blue channel, 0 to 255.</param>
/// <param name="Alpha">The opacity, 0 to 1; 1 when the colour gives none.</param>
/// <param name="Notation">The notation the colour was written in.</param>
public sealed record ParsedColor(int Red, int Green, int Blue, double Alpha, ColorNotation Notation);

/// <summary>Reads a colour on its own from text and writes it in another notation.</summary>
public static partial class ConvertColor
{
    /// <summary>Reads the text, without surrounding white space, as exactly one colour; nothing when it is not one.</summary>
    /// <param name="text">The text to read.</param>
    /// <returns>The colour, or null when the text is not exactly one valid colour.</returns>
    public static ParsedColor? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var value = text.Trim().ToLowerInvariant();
        if (value.StartsWith('#'))
        {
            return ParseHex(value[1..]);
        }

        return ParseFunction(value);
    }

    /// <summary>Writes the colour in the given notation.</summary>
    /// <param name="color">The colour to write.</param>
    /// <param name="notation">The notation to write it in.</param>
    /// <returns>The colour as text.</returns>
    public static string Format(ParsedColor color, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(color);

        var hasAlpha = color.Alpha < 1;
        switch (notation)
        {
            case ColorNotation.Hex:
                var hex = string.Create(CultureInfo.InvariantCulture, $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}");
                return hasAlpha
                    ? hex + RoundHalfUp(color.Alpha * 255).ToString("x2", CultureInfo.InvariantCulture)
                    : hex;
            case ColorNotation.Rgb:
                return hasAlpha
                    ? string.Create(CultureInfo.InvariantCulture, $"rgba({color.Red}, {color.Green}, {color.Blue}, {FormatAlpha(color.Alpha)})")
                    : string.Create(CultureInfo.InvariantCulture, $"rgb({color.Red}, {color.Green}, {color.Blue})");
            case ColorNotation.Hsl:
                ToHsl(color, out var hue, out var saturation, out var lightness);
                return hasAlpha
                    ? string.Create(CultureInfo.InvariantCulture, $"hsla({hue}, {saturation}%, {lightness}%, {FormatAlpha(color.Alpha)})")
                    : string.Create(CultureInfo.InvariantCulture, $"hsl({hue}, {saturation}%, {lightness}%)");
            default:
                throw new ArgumentOutOfRangeException(nameof(notation));
        }
    }

    internal static bool CanExecute(ClipboardItem item, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text
            && Parse(item.Snapshot.Text) is { } color
            && color.Notation != notation;
    }

    internal static string Describe(ClipboardItem item, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Parse(item.Snapshot.Text) is { } color
            ? Localizer.Format(Strings.ActionColorDescription, Format(color, notation))
            : Strings.ActionColorDescription;
    }

    internal static Task PasteAsync(ClipboardItem item, ActionContext context, ColorNotation notation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (Parse(item.Snapshot.Text) is not { } color)
        {
            return Task.CompletedTask;
        }

        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Format(color, notation) }, cancellationToken);
    }

    private static ParsedColor? ParseHex(string digits)
    {
        if (digits.Length is not (3 or 4 or 6 or 8))
        {
            return null;
        }

        foreach (var c in digits)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return null;
            }
        }

        if (digits.Length is 3 or 4)
        {
            digits = string.Concat(digits.Select(c => new string(c, 2)));
        }

        var red = int.Parse(digits[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var green = int.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var blue = int.Parse(digits[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var alpha = digits.Length == 8
            ? int.Parse(digits[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0
            : 1.0;
        return new ParsedColor(red, green, blue, alpha, ColorNotation.Hex);
    }

    private static ParsedColor? ParseFunction(string text)
    {
        ColorNotation notation;
        string inner;
        if (TryStrip(text, "rgb(", out inner) || TryStrip(text, "rgba(", out inner))
        {
            notation = ColorNotation.Rgb;
        }
        else if (TryStrip(text, "hsl(", out inner) || TryStrip(text, "hsla(", out inner))
        {
            notation = ColorNotation.Hsl;
        }
        else
        {
            return null;
        }

        var values = SplitValues(inner);
        if (values is null)
        {
            return null;
        }

        var alpha = 1.0;
        if (values.Length == 4 && !TryParseAlpha(values[3], out alpha))
        {
            return null;
        }

        if (notation == ColorNotation.Rgb)
        {
            if (!TryParseChannel(values[0], out var red)
                || !TryParseChannel(values[1], out var green)
                || !TryParseChannel(values[2], out var blue))
            {
                return null;
            }

            return new ParsedColor(red, green, blue, alpha, ColorNotation.Rgb);
        }

        if (!TryParseHue(values[0], out var hue)
            || !TryParsePercentage(values[1], out var saturation)
            || !TryParsePercentage(values[2], out var lightness))
        {
            return null;
        }

        return FromHsl(hue, saturation, lightness, alpha);
    }

    private static bool TryStrip(string text, string prefix, out string inner)
    {
        if (text.StartsWith(prefix, StringComparison.Ordinal) && text.EndsWith(')'))
        {
            inner = text[prefix.Length..^1];
            return true;
        }

        inner = string.Empty;
        return false;
    }

    private static string[]? SplitValues(string inner)
    {
        if (inner.Contains(','))
        {
            var parts = inner.Split(',');
            if (parts.Length is not (3 or 4))
            {
                return null;
            }

            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim();
            }

            return parts;
        }

        var halves = inner.Split('/');
        if (halves.Length > 2)
        {
            return null;
        }

        var values = halves[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 3)
        {
            return null;
        }

        if (halves.Length == 1)
        {
            return values;
        }

        return [.. values, halves[1].Trim()];
    }

    private static bool TryParseNumber(string text, out double value)
    {
        if (!NumberPattern().IsMatch(text))
        {
            value = 0;
            return false;
        }

        value = double.Parse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParsePercentage(string text, out double value)
    {
        value = 0;
        return text.EndsWith('%') && TryParseNumber(text[..^1], out value) && value <= 100;
    }

    private static bool TryParseChannel(string text, out int value)
    {
        value = 0;
        if (text.EndsWith('%'))
        {
            if (!TryParsePercentage(text, out var percentage))
            {
                return false;
            }

            value = RoundHalfUp(percentage * 255 / 100);
            return true;
        }

        if (!TryParseNumber(text, out var number) || number > 255)
        {
            return false;
        }

        value = RoundHalfUp(number);
        return true;
    }

    private static bool TryParseHue(string text, out double value)
    {
        if (text.EndsWith("deg", StringComparison.Ordinal))
        {
            text = text[..^3];
        }

        return TryParseNumber(text, out value);
    }

    private static bool TryParseAlpha(string text, out double value)
    {
        if (text.EndsWith('%'))
        {
            if (!TryParsePercentage(text, out var percentage))
            {
                value = 0;
                return false;
            }

            value = percentage / 100;
            return true;
        }

        return TryParseNumber(text, out value) && value <= 1;
    }

    private static ParsedColor FromHsl(double hue, double saturation, double lightness, double alpha)
    {
        hue %= 360;
        var s = saturation / 100;
        var l = lightness / 100;
        var a = s * Math.Min(l, 1 - l);

        int Channel(double n)
        {
            var k = (n + hue / 30) % 12;
            var value = l - a * Math.Max(-1, Math.Min(Math.Min(k - 3, 9 - k), 1));
            return RoundHalfUp(value * 255);
        }

        return new ParsedColor(Channel(0), Channel(8), Channel(4), alpha, ColorNotation.Hsl);
    }

    private static void ToHsl(ParsedColor color, out int hue, out int saturation, out int lightness)
    {
        var r = color.Red / 255.0;
        var g = color.Green / 255.0;
        var b = color.Blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2;
        double h = 0;
        double s = 0;
        if (max != min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r)
            {
                h = (g - b) / d;
                if (h < 0)
                {
                    h += 6;
                }
            }
            else if (max == g)
            {
                h = (b - r) / d + 2;
            }
            else
            {
                h = (r - g) / d + 4;
            }

            h *= 60;
        }

        hue = RoundHalfUp(h) % 360;
        saturation = RoundHalfUp(s * 100);
        lightness = RoundHalfUp(l * 100);
    }

    private static string FormatAlpha(double alpha) =>
        Math.Round(alpha, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    private static int RoundHalfUp(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"^(?:\d+(?:\.\d+)?|\.\d+)\z")]
    private static partial Regex NumberPattern();
}

/// <summary>The action that pastes a copied colour as a hex colour.</summary>
public sealed class PasteAsHexColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-hex";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 46;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHex;
    }

    /// <summary>Gets the description, showing the colour that will be pasted.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item) => ConvertColor.Describe(item, ColorNotation.Hex);

    /// <summary>Tells whether the item is a colour on its own that is not already hex.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ConvertColor.CanExecute(item, ColorNotation.Hex);

    /// <summary>Pastes the colour as a hex colour, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ConvertColor.PasteAsync(item, context, ColorNotation.Hex, cancellationToken);
}

/// <summary>The action that pastes a copied colour as an RGB colour.</summary>
public sealed class PasteAsRgbColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-rgb";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 47;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorRgb;
    }

    /// <summary>Gets the description, showing the colour that will be pasted.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item) => ConvertColor.Describe(item, ColorNotation.Rgb);

    /// <summary>Tells whether the item is a colour on its own that is not already RGB.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ConvertColor.CanExecute(item, ColorNotation.Rgb);

    /// <summary>Pastes the colour as an RGB colour, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ConvertColor.PasteAsync(item, context, ColorNotation.Rgb, cancellationToken);
}

/// <summary>The action that pastes a copied colour as an HSL colour.</summary>
public sealed class PasteAsHslColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-hsl";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 48;

    /// <summary>Gets the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHsl;
    }

    /// <summary>Gets the description, showing the colour that will be pasted.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item) => ConvertColor.Describe(item, ColorNotation.Hsl);

    /// <summary>Tells whether the item is a colour on its own that is not already HSL.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ConvertColor.CanExecute(item, ColorNotation.Hsl);

    /// <summary>Pastes the colour as an HSL colour, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context to paste with.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ConvertColor.PasteAsync(item, context, ColorNotation.Hsl, cancellationToken);
}
