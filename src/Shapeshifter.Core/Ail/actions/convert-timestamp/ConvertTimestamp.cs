using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Reads and writes moments as Unix times and as ISO 8601 dates and times.
/// </summary>
public static partial class ConvertTimestamp
{
    /// <summary>
    /// Reads a Unix time: 10 digits of seconds from 1000000000 on, or 13 digits of milliseconds
    /// from 1000000000000 on, with white space allowed at the start and end.
    /// </summary>
    /// <param name="text">The text to read, or null.</param>
    /// <returns>The moment and whether it was given in milliseconds, or null when the text is not such a Unix time.</returns>
    public static (DateTimeOffset Instant, bool Milliseconds)? ParseUnix(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length != 10 && trimmed.Length != 13)
        {
            return null;
        }

        foreach (var c in trimmed)
        {
            if (!char.IsAsciiDigit(c))
            {
                return null;
            }
        }

        var value = long.Parse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture);
        if (trimmed.Length == 10)
        {
            if (value < 1000000000L)
            {
                return null;
            }

            return (DateTimeOffset.FromUnixTimeSeconds(value), false);
        }

        if (value < 1000000000000L)
        {
            return null;
        }

        return (DateTimeOffset.FromUnixTimeMilliseconds(value), true);
    }

    /// <summary>
    /// Reads an ISO 8601 date and time, with white space allowed at the start and end; without Z or
    /// an offset it is read as a time on the computer's clock, in its time zone at that moment.
    /// </summary>
    /// <param name="text">The text to read, or null.</param>
    /// <returns>The moment, or null when the text is not an ISO 8601 date with a time.</returns>
    public static DateTimeOffset? ParseDate(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var match = DatePattern().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        var year = ParseGroup(match, "y");
        var month = ParseGroup(match, "mo");
        var day = ParseGroup(match, "d");
        var hour = ParseGroup(match, "h");
        var minute = ParseGroup(match, "mi");
        var second = match.Groups["s"].Success ? ParseGroup(match, "s") : 0;

        long fractionTicks = 0;
        if (match.Groups["f"].Success)
        {
            var fraction = match.Groups["f"].Value;
            if (fraction.Length > 7)
            {
                fraction = fraction[..7];
            }

            fractionTicks = long.Parse(fraction.PadRight(7, '0'), NumberStyles.None, CultureInfo.InvariantCulture);
        }

        try
        {
            var dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(fractionTicks);

            TimeSpan offset;
            if (!match.Groups["z"].Success)
            {
                offset = TimeZoneInfo.Local.GetUtcOffset(dateTime);
            }
            else if (match.Groups["z"].Value == "Z")
            {
                offset = TimeSpan.Zero;
            }
            else
            {
                var offsetHours = ParseGroup(match, "oh");
                var offsetMinutes = match.Groups["om"].Success ? ParseGroup(match, "om") : 0;
                if (offsetMinutes > 59)
                {
                    return null;
                }

                offset = new TimeSpan(offsetHours, offsetMinutes, 0);
                if (match.Groups["sign"].Value == "-")
                {
                    offset = offset.Negate();
                }
            }

            return new DateTimeOffset(dateTime, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a moment as "yyyy-MM-ddTHH:mm:ss" at the given offset, with ".fff" when asked for
    /// milliseconds, then "Z" for a zero offset or the offset as "+HH:mm" or "-HH:mm".
    /// </summary>
    /// <param name="instant">The moment to write.</param>
    /// <param name="offset">The offset from UTC to write it at.</param>
    /// <param name="milliseconds">Whether to write the milliseconds.</param>
    /// <returns>The ISO 8601 text.</returns>
    public static string FormatDate(DateTimeOffset instant, TimeSpan offset, bool milliseconds)
    {
        var local = instant.ToOffset(offset);
        var text = local.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss", CultureInfo.InvariantCulture);
        if (milliseconds)
        {
            text += local.ToString("'.'fff", CultureInfo.InvariantCulture);
        }

        if (offset == TimeSpan.Zero)
        {
            return text + "Z";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return text + sign + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Writes a moment as the whole seconds since 1970-01-01T00:00:00Z, rounded down.
    /// </summary>
    /// <param name="instant">The moment to write.</param>
    /// <returns>The Unix time as digits.</returns>
    public static string FormatUnix(DateTimeOffset instant)
    {
        return instant.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    }

    private static int ParseGroup(Match match, string name)
    {
        return int.Parse(match.Groups[name].Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^(?<y>[0-9]{4})-(?<mo>[0-9]{2})-(?<d>[0-9]{2})[T ](?<h>[0-9]{2}):(?<mi>[0-9]{2})(?::(?<s>[0-9]{2})(?:\.(?<f>[0-9]+))?)?(?<z>Z|(?<sign>[+-])(?<oh>[0-9]{2})(?::?(?<om>[0-9]{2}))?)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}

/// <summary>
/// The action that pastes a copied Unix time as an ISO 8601 date and time in the computer's time zone.
/// </summary>
public sealed class PasteAsDateAction : IClipboardAction
{
    /// <summary>The action's id.</summary>
    public string Id => "paste-as-date";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 49;

    /// <summary>The action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsDate;
    }

    /// <summary>The action's description, showing what it pastes.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Result(item));
    }

    /// <summary>Whether the item is text holding a Unix time.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ConvertTimestamp.ParseUnix(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the date and time, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The action's context.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when it has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Result(item) }, cancellationToken);
    }

    private static string Result(ClipboardItem item)
    {
        var parsed = ConvertTimestamp.ParseUnix(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item does not hold a Unix time.");
        var offset = TimeZoneInfo.Local.GetUtcOffset(parsed.Instant);
        return ConvertTimestamp.FormatDate(parsed.Instant, offset, parsed.Milliseconds);
    }
}

/// <summary>
/// The action that pastes a copied ISO 8601 date and time as a Unix time in seconds.
/// </summary>
public sealed class PasteAsUnixTimeAction : IClipboardAction
{
    /// <summary>The action's id.</summary>
    public string Id => "paste-as-unix-time";

    /// <summary>Where the action is listed among the actions.</summary>
    public int Order => 50;

    /// <summary>The action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsUnixTime;
    }

    /// <summary>The action's description, showing what it pastes.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Result(item));
    }

    /// <summary>Whether the item is text holding an ISO 8601 date and time.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ConvertTimestamp.ParseDate(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the Unix time, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The action's context.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when it has been pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Result(item) }, cancellationToken);
    }

    private static string Result(ClipboardItem item)
    {
        var instant = ConvertTimestamp.ParseDate(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item does not hold an ISO 8601 date and time.");
        return ConvertTimestamp.FormatUnix(instant);
    }
}
