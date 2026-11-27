using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Buffers;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shapeshifter.Core.Ail;

/// <summary>The notation a colour is written in.</summary>
public enum ColorNotation
{
    /// <summary>A hexadecimal colour, such as #3584e4.</summary>
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

/// <summary>Reads colours written as hex, rgb() or hsl() and writes them in any of those notations.</summary>
public static class ConvertColor
{
    private static readonly Regex HexPattern = new(
        "^#([0-9a-f]{3}|[0-9a-f]{4}|[0-9a-f]{6}|[0-9a-f]{8})$",
        RegexOptions.CultureInvariant);

    private static readonly Regex NumberPattern = new(
        "^([0-9]+(\\.[0-9]+)?|\\.[0-9]+)$",
        RegexOptions.CultureInvariant);

    private static readonly Regex HuePattern = new(
        "^(-?([0-9]+(\\.[0-9]+)?|\\.[0-9]+))(deg)?$",
        RegexOptions.CultureInvariant);

    private static readonly char[] Spaces = [' ', '\t', '\n', '\r', '\f', '\v'];

    private static readonly SearchValues<char> SpaceValues = SearchValues.Create(" \t\n\r\f\v");

    /// <summary>Reads text that is exactly one colour, or gives null when it is none.</summary>
    /// <param name="text">The text to read; may be null.</param>
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
            return ParseHex(value);
        }

        if (TryFunction(value, "rgba(", out var inner) || TryFunction(value, "rgb(", out inner))
        {
            return ParseRgb(inner);
        }

        if (TryFunction(value, "hsla(", out inner) || TryFunction(value, "hsl(", out inner))
        {
            return ParseHsl(inner);
        }

        return null;
    }

    /// <summary>Writes a colour in the given notation.</summary>
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
                var hex = string.Create(
                    CultureInfo.InvariantCulture,
                    $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}");
                return hasAlpha
                    ? hex + RoundHalfUp(color.Alpha * 255).ToString("x2", CultureInfo.InvariantCulture)
                    : hex;
            case ColorNotation.Rgb:
                return hasAlpha
                    ? string.Create(CultureInfo.InvariantCulture, $"rgba({color.Red}, {color.Green}, {color.Blue}, {FormatAlpha(color.Alpha)})")
                    : string.Create(CultureInfo.InvariantCulture, $"rgb({color.Red}, {color.Green}, {color.Blue})");
            default:
                var (hue, saturation, lightness) = ToHsl(color);
                return hasAlpha
                    ? string.Create(CultureInfo.InvariantCulture, $"hsla({hue}, {saturation}%, {lightness}%, {FormatAlpha(color.Alpha)})")
                    : string.Create(CultureInfo.InvariantCulture, $"hsl({hue}, {saturation}%, {lightness}%)");
        }
    }

    private static ParsedColor? ParseHex(string value)
    {
        if (!HexPattern.IsMatch(value))
        {
            return null;
        }

        var digits = value[1..];
        if (digits.Length <= 4)
        {
            digits = string.Concat(digits.Select(digit => new string(digit, 2)));
        }

        var red = int.Parse(digits[0..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var green = int.Parse(digits[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var blue = int.Parse(digits[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var alpha = digits.Length == 8
            ? int.Parse(digits[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0
            : 1.0;
        return new ParsedColor(red, green, blue, alpha, ColorNotation.Hex);
    }

    private static ParsedColor? ParseRgb(string inner)
    {
        if (!TrySplit(inner, out var values, out var alphaText))
        {
            return null;
        }

        var channels = new int[3];
        for (var index = 0; index < 3; index++)
        {
            var part = values[index];
            if (part.EndsWith('%'))
            {
                if (!TryNumber(part[..^1], out var percent) || percent > 100)
                {
                    return null;
                }

                channels[index] = RoundHalfUp(percent * 255 / 100);
            }
            else
            {
                if (!TryNumber(part, out var number) || number > 255)
                {
                    return null;
                }

                channels[index] = RoundHalfUp(number);
            }
        }

        if (!TryAlpha(alphaText, out var alpha))
        {
            return null;
        }

        return new ParsedColor(channels[0], channels[1], channels[2], alpha, ColorNotation.Rgb);
    }

    private static ParsedColor? ParseHsl(string inner)
    {
        if (!TrySplit(inner, out var values, out var alphaText))
        {
            return null;
        }

        var hueMatch = HuePattern.Match(values[0]);
        if (!hueMatch.Success)
        {
            return null;
        }

        var hue = double.Parse(hueMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
        if (!TryPercent(values[1], out var saturation) || !TryPercent(values[2], out var lightness))
        {
            return null;
        }

        if (!TryAlpha(alphaText, out var alpha))
        {
            return null;
        }

        hue %= 360;
        if (hue < 0)
        {
            hue += 360;
        }

        var s = saturation / 100;
        var l = lightness / 100;
        var a = s * Math.Min(l, 1 - l);
        double Channel(double n)
        {
            var k = (n + (hue / 30)) % 12;
            return l - (a * Math.Max(-1, Math.Min(Math.Min(k - 3, 9 - k), 1)));
        }

        return new ParsedColor(
            RoundHalfUp(Channel(0) * 255),
            RoundHalfUp(Channel(8) * 255),
            RoundHalfUp(Channel(4) * 255),
            alpha,
            ColorNotation.Hsl);
    }

    private static bool TryFunction(string value, string prefix, out string inner)
    {
        if (value.StartsWith(prefix, StringComparison.Ordinal) && value.EndsWith(')'))
        {
            inner = value[prefix.Length..^1];
            return true;
        }

        inner = string.Empty;
        return false;
    }

    private static bool HasSpace(string text) => text.AsSpan().ContainsAny(SpaceValues);

    private static bool TrySplit(string inner, out string[] values, out string? alphaText)
    {
        values = [];
        alphaText = null;

        if (inner.Contains(','))
        {
            if (inner.Contains('/'))
            {
                return false;
            }

            var parts = inner.Split(',').Select(part => part.Trim()).ToArray();
            if (parts.Length is not (3 or 4) || parts.Any(part => part.Length == 0 || HasSpace(part)))
            {
                return false;
            }

            values = parts[..3];
            alphaText = parts.Length == 4 ? parts[3] : null;
            return true;
        }

        var halves = inner.Split('/');
        if (halves.Length > 2)
        {
            return false;
        }

        var spaced = halves[0].Split(Spaces, StringSplitOptions.RemoveEmptyEntries);
        if (spaced.Length != 3)
        {
            return false;
        }

        if (halves.Length == 2)
        {
            var alpha = halves[1].Trim();
            if (alpha.Length == 0 || HasSpace(alpha))
            {
                return false;
            }

            alphaText = alpha;
        }

        values = spaced;
        return true;
    }

    private static bool TryNumber(string text, out double number)
    {
        number = 0;
        return NumberPattern.IsMatch(text)
            && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryPercent(string text, out double percent)
    {
        percent = 0;
        return text.EndsWith('%') && TryNumber(text[..^1], out percent) && percent <= 100;
    }

    private static bool TryAlpha(string? text, out double alpha)
    {
        alpha = 1;
        if (text is null)
        {
            return true;
        }

        if (text.EndsWith('%'))
        {
            if (!TryPercent(text, out var percent))
            {
                return false;
            }

            alpha = percent / 100;
            return true;
        }

        return TryNumber(text, out alpha) && alpha <= 1;
    }

    private static (int Hue, int Saturation, int Lightness) ToHsl(ParsedColor color)
    {
        var r = color.Red / 255.0;
        var g = color.Green / 255.0;
        var b = color.Blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;
        var saturation = delta == 0 ? 0 : delta / (1 - Math.Abs((2 * lightness) - 1));

        double hue;
        if (delta == 0)
        {
            hue = 0;
        }
        else if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }

        if (hue < 0)
        {
            hue += 360;
        }

        var roundedHue = RoundHalfUp(hue);
        if (roundedHue == 360)
        {
            roundedHue = 0;
        }

        return (roundedHue, RoundHalfUp(saturation * 100), RoundHalfUp(lightness * 100));
    }

    private static string FormatAlpha(double alpha) =>
        Math.Round(alpha, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    private static int RoundHalfUp(double value) =>
        (int)Math.Round(value, MidpointRounding.AwayFromZero);
}

/// <summary>Shared behaviour of the actions that paste a copied colour in another notation.</summary>
internal static class ColorActions
{
    public static bool CanExecute(ClipboardItem item, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text
            && ConvertColor.Parse(item.Snapshot.Text) is { } color
            && color.Notation != notation;
    }

    public static string GetDescription(ClipboardItem item, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(item);
        var color = ConvertColor.Parse(item.Snapshot.Text);
        return color is null
            ? Strings.ActionColorDescription
            : Localizer.Format(Strings.ActionColorDescription, ConvertColor.Format(color, notation));
    }

    public static async Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        ColorNotation notation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var color = ConvertColor.Parse(item.Snapshot.Text);
        if (color is null)
        {
            return;
        }

        await context.WriteAndPasteAsync(
            new ClipboardSnapshot { Text = ConvertColor.Format(color, notation) },
            cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Pastes a copied rgb() or hsl() colour as a hex colour.</summary>
public sealed class PasteAsHexColorAction : IClipboardAction
{
    /// <summary>The action's stable id.</summary>
    public string Id => "paste-color-hex";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 46;

    /// <summary>Gives the action's title for the item.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHex;
    }

    /// <summary>Gives the action's description for the item, showing the colour it will paste.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item) => ColorActions.GetDescription(item, ColorNotation.Hex);

    /// <summary>Tells whether the item is a colour not already written in hex.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ColorActions.CanExecute(item, ColorNotation.Hex);

    /// <summary>Pastes the item's colour as a hex colour.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour is pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ColorActions.ExecuteAsync(item, context, ColorNotation.Hex, cancellationToken);
}

/// <summary>Pastes a copied hex or hsl() colour as an rgb() colour.</summary>
public sealed class PasteAsRgbColorAction : IClipboardAction
{
    /// <summary>The action's stable id.</summary>
    public string Id => "paste-color-rgb";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 47;

    /// <summary>Gives the action's title for the item.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorRgb;
    }

    /// <summary>Gives the action's description for the item, showing the colour it will paste.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item) => ColorActions.GetDescription(item, ColorNotation.Rgb);

    /// <summary>Tells whether the item is a colour not already written as rgb().</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ColorActions.CanExecute(item, ColorNotation.Rgb);

    /// <summary>Pastes the item's colour as an rgb() colour.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour is pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ColorActions.ExecuteAsync(item, context, ColorNotation.Rgb, cancellationToken);
}

/// <summary>Pastes a copied hex or rgb() colour as an hsl() colour.</summary>
public sealed class PasteAsHslColorAction : IClipboardAction
{
    /// <summary>The action's stable id.</summary>
    public string Id => "paste-color-hsl";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 48;

    /// <summary>Gives the action's title for the item.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHsl;
    }

    /// <summary>Gives the action's description for the item, showing the colour it will paste.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item) => ColorActions.GetDescription(item, ColorNotation.Hsl);

    /// <summary>Tells whether the item is a colour not already written as hsl().</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies.</returns>
    public bool CanExecute(ClipboardItem item) => ColorActions.CanExecute(item, ColorNotation.Hsl);

    /// <summary>Pastes the item's colour as an hsl() colour.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the colour is pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default) =>
        ColorActions.ExecuteAsync(item, context, ColorNotation.Hsl, cancellationToken);
}
