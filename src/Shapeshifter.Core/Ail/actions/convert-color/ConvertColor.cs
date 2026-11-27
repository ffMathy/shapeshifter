using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shapeshifter.Core.Ail;

/// <summary>A notation a colour can be written in.</summary>
public enum ColorNotation
{
    /// <summary>A hexadecimal colour such as #3584e4.</summary>
    Hex,

    /// <summary>An rgb() or rgba() colour.</summary>
    Rgb,

    /// <summary>An hsl() or hsla() colour.</summary>
    Hsl,
}

/// <summary>A colour read from a text, with the notation it was written in.</summary>
public sealed record ParsedColor
{
    /// <summary>The red channel, 0 to 255.</summary>
    public int Red { get; init; }

    /// <summary>The green channel, 0 to 255.</summary>
    public int Green { get; init; }

    /// <summary>The blue channel, 0 to 255.</summary>
    public int Blue { get; init; }

    /// <summary>The opacity, 0 to 1; 1 when the colour gives none.</summary>
    public double Alpha { get; init; }

    /// <summary>The notation the colour was written in.</summary>
    public ColorNotation Notation { get; init; }
}

/// <summary>Reads a colour written on its own and writes it in another notation.</summary>
public static partial class ConvertColor
{
    /// <summary>Reads the text as exactly one hex, rgb() or hsl() colour, or gives null when it is none.</summary>
    public static ParsedColor? Parse(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith('#'))
        {
            return ParseHex(trimmed[1..]);
        }

        var open = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (open < 0 || !trimmed.EndsWith(')'))
        {
            return null;
        }

        var name = trimmed[..open];
        var values = SplitValues(trimmed[(open + 1)..^1]);
        if (values is null)
        {
            return null;
        }

        if (string.Equals(name, "rgb", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "rgba", StringComparison.OrdinalIgnoreCase))
        {
            return ParseRgb(values);
        }

        if (string.Equals(name, "hsl", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "hsla", StringComparison.OrdinalIgnoreCase))
        {
            return ParseHsl(values);
        }

        return null;
    }

    /// <summary>Writes the colour in the given notation, the way CSS writes it.</summary>
    public static string Format(ParsedColor color, ColorNotation notation)
    {
        ArgumentNullException.ThrowIfNull(color);

        var hasAlpha = color.Alpha < 1;
        switch (notation)
        {
            case ColorNotation.Hex:
                var hex = string.Create(CultureInfo.InvariantCulture, $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}");
                return hasAlpha
                    ? hex + string.Create(CultureInfo.InvariantCulture, $"{RoundHalfUp(color.Alpha * 255):x2}")
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

    internal static bool CanConvert(ClipboardItem item, ColorNotation notation)
    {
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        var color = Parse(item.Snapshot.Text);
        return color is not null && color.Notation != notation;
    }

    internal static string Describe(ClipboardItem item, ColorNotation notation)
    {
        var color = Parse(item.Snapshot.Text);
        return color is null
            ? Strings.ActionColorDescription
            : Localizer.Format(Strings.ActionColorDescription, Format(color, notation));
    }

    internal static Task PasteAsync(ClipboardItem item, ActionContext context, ColorNotation notation, CancellationToken cancellationToken)
    {
        var color = Parse(item.Snapshot.Text);
        if (color is null)
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
            var expanded = new char[digits.Length * 2];
            for (var i = 0; i < digits.Length; i++)
            {
                expanded[i * 2] = digits[i];
                expanded[(i * 2) + 1] = digits[i];
            }

            digits = new string(expanded);
        }

        var alpha = digits.Length == 8 ? HexByte(digits, 6) / 255.0 : 1.0;
        return new ParsedColor
        {
            Red = HexByte(digits, 0),
            Green = HexByte(digits, 2),
            Blue = HexByte(digits, 4),
            Alpha = alpha,
            Notation = ColorNotation.Hex,
        };
    }

    private static int HexByte(string digits, int start) =>
        int.Parse(digits.AsSpan(start, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);

    private static string[]? SplitValues(string inner)
    {
        if (inner.Contains(',', StringComparison.Ordinal))
        {
            if (inner.Contains('/', StringComparison.Ordinal))
            {
                return null;
            }

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

        var main = halves[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (main.Length != 3)
        {
            return null;
        }

        if (halves.Length == 1)
        {
            return main;
        }

        return [main[0], main[1], main[2], halves[1].Trim()];
    }

    private static ParsedColor? ParseRgb(string[] values)
    {
        var red = Channel(values[0]);
        var green = Channel(values[1]);
        var blue = Channel(values[2]);
        var alpha = values.Length == 4 ? Alpha(values[3]) : 1.0;
        if (red is null || green is null || blue is null || alpha is null)
        {
            return null;
        }

        return new ParsedColor
        {
            Red = RoundHalfUp(red.Value),
            Green = RoundHalfUp(green.Value),
            Blue = RoundHalfUp(blue.Value),
            Alpha = alpha.Value,
            Notation = ColorNotation.Rgb,
        };
    }

    private static ParsedColor? ParseHsl(string[] values)
    {
        var hue = Hue(values[0]);
        var saturation = Percent(values[1]);
        var lightness = Percent(values[2]);
        var alpha = values.Length == 4 ? Alpha(values[3]) : 1.0;
        if (hue is null || saturation is null || lightness is null || alpha is null)
        {
            return null;
        }

        var h = ((hue.Value % 360) + 360) % 360;
        var s = saturation.Value;
        var l = lightness.Value;
        var chroma = (1 - Math.Abs((2 * l) - 1)) * s;
        var sector = h / 60;
        var x = chroma * (1 - Math.Abs((sector % 2) - 1));
        var (r, g, b) = sector switch
        {
            < 1 => (chroma, x, 0.0),
            < 2 => (x, chroma, 0.0),
            < 3 => (0.0, chroma, x),
            < 4 => (0.0, x, chroma),
            < 5 => (x, 0.0, chroma),
            _ => (chroma, 0.0, x),
        };
        var m = l - (chroma / 2);

        return new ParsedColor
        {
            Red = RoundHalfUp((r + m) * 255),
            Green = RoundHalfUp((g + m) * 255),
            Blue = RoundHalfUp((b + m) * 255),
            Alpha = alpha.Value,
            Notation = ColorNotation.Hsl,
        };
    }

    private static double? Channel(string token)
    {
        if (token.EndsWith('%'))
        {
            var percent = Number(token[..^1]);
            return percent is >= 0 and <= 100 ? percent.Value * 255 / 100 : null;
        }

        var value = Number(token);
        return value is >= 0 and <= 255 ? value : null;
    }

    private static double? Percent(string token)
    {
        if (!token.EndsWith('%'))
        {
            return null;
        }

        var percent = Number(token[..^1]);
        return percent is >= 0 and <= 100 ? percent.Value / 100 : null;
    }

    private static double? Alpha(string token)
    {
        if (token.EndsWith('%'))
        {
            return Percent(token);
        }

        var value = Number(token);
        return value is >= 0 and <= 1 ? value : null;
    }

    private static double? Hue(string token)
    {
        if (!HuePattern().IsMatch(token))
        {
            return null;
        }

        var number = token.EndsWith("deg", StringComparison.OrdinalIgnoreCase) ? token[..^3] : token;
        return double.Parse(number, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    private static double? Number(string token) =>
        NumberPattern().IsMatch(token)
            ? double.Parse(token, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
            : null;

    private static (int Hue, int Saturation, int Lightness) ToHsl(ParsedColor color)
    {
        var r = color.Red / 255.0;
        var g = color.Green / 255.0;
        var b = color.Blue / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;
        var lightness = (max + min) / 2;
        double hue = 0;
        double saturation = 0;
        if (delta > 0)
        {
            saturation = delta / (1 - Math.Abs((2 * lightness) - 1));
            if (max == r)
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
        }

        return (RoundHalfUp(hue) % 360, RoundHalfUp(saturation * 100), RoundHalfUp(lightness * 100));
    }

    private static string FormatAlpha(double alpha) =>
        Math.Round(alpha, 2, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture);

    private static int RoundHalfUp(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

    [GeneratedRegex("^(?:[0-9]+(?:\\.[0-9]+)?|\\.[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberPattern();

    [GeneratedRegex("^[+-]?(?:[0-9]+(?:\\.[0-9]+)?|\\.[0-9]+)(?:deg)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex HuePattern();
}

/// <summary>Pastes a colour copied as rgb() or hsl() as a hex colour.</summary>
public sealed class PasteAsHexColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-hex";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 46;

    /// <summary>The action's title, shown in the actions.</summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHex;
    }

    /// <summary>Says which colour will be pasted.</summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.Describe(item, ColorNotation.Hex);
    }

    /// <summary>Whether the item is a colour on its own, written in another notation.</summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.CanConvert(item, ColorNotation.Hex);
    }

    /// <summary>Pastes the item's colour as a hex colour, without adding it to the history.</summary>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return ConvertColor.PasteAsync(item, context, ColorNotation.Hex, cancellationToken);
    }
}

/// <summary>Pastes a colour copied as hex or hsl() as an rgb() colour.</summary>
public sealed class PasteAsRgbColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-rgb";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 47;

    /// <summary>The action's title, shown in the actions.</summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorRgb;
    }

    /// <summary>Says which colour will be pasted.</summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.Describe(item, ColorNotation.Rgb);
    }

    /// <summary>Whether the item is a colour on its own, written in another notation.</summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.CanConvert(item, ColorNotation.Rgb);
    }

    /// <summary>Pastes the item's colour as an rgb() colour, without adding it to the history.</summary>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return ConvertColor.PasteAsync(item, context, ColorNotation.Rgb, cancellationToken);
    }
}

/// <summary>Pastes a colour copied as hex or rgb() as an hsl() colour.</summary>
public sealed class PasteAsHslColorAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-color-hsl";

    /// <summary>Where the action is placed among the actions.</summary>
    public int Order => 48;

    /// <summary>The action's title, shown in the actions.</summary>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionColorHsl;
    }

    /// <summary>Says which colour will be pasted.</summary>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.Describe(item, ColorNotation.Hsl);
    }

    /// <summary>Whether the item is a colour on its own, written in another notation.</summary>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ConvertColor.CanConvert(item, ColorNotation.Hsl);
    }

    /// <summary>Pastes the item's colour as an hsl() colour, without adding it to the history.</summary>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return ConvertColor.PasteAsync(item, context, ColorNotation.Hsl, cancellationToken);
    }
}
