using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Reads and writes moments as Unix times and as ISO 8601 dates and times.
/// </summary>
public static partial class ConvertTimestamp
{
    /// <summary>
    /// Reads text that is only a Unix time: 10 digits of seconds or 13 digits of milliseconds since 1970-01-01T00:00:00Z, from 2001 on.
    /// </summary>
    /// <param name="text">The text to read; white space at its start and end is ignored.</param>
    /// <returns>The moment and whether it was given in milliseconds, or <see langword="null"/> when the text is not such a Unix time.</returns>
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

        foreach (var character in trimmed)
        {
            if (!char.IsAsciiDigit(character))
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
    /// Reads text that is an ISO 8601 date with a time, with an optional Z or offset; without one, it is read as local time.
    /// </summary>
    /// <param name="text">The text to read; white space at its start and end is ignored.</param>
    /// <returns>The moment, or <see langword="null"/> when the text is not an ISO 8601 date and time.</returns>
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

        var year = ParseNumber(match.Groups["year"].Value);
        var month = ParseNumber(match.Groups["month"].Value);
        var day = ParseNumber(match.Groups["day"].Value);
        var hour = ParseNumber(match.Groups["hour"].Value);
        var minute = ParseNumber(match.Groups["minute"].Value);
        var second = match.Groups["second"].Success ? ParseNumber(match.Groups["second"].Value) : 0;

        if (year < 1 || month < 1 || month > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)
            || hour > 23 || minute > 59 || second > 59)
        {
            return null;
        }

        long fractionTicks = 0;
        if (match.Groups["fraction"].Success)
        {
            var digits = match.Groups["fraction"].Value;
            if (digits.Length > 7)
            {
                digits = digits[..7];
            }

            fractionTicks = ParseNumber(digits.PadRight(7, '0'));
        }

        var clock = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(fractionTicks);

        TimeSpan offset;
        if (match.Groups["zulu"].Success)
        {
            offset = TimeSpan.Zero;
        }
        else if (match.Groups["sign"].Success)
        {
            var offsetHours = ParseNumber(match.Groups["offsetHours"].Value);
            var offsetMinutes = match.Groups["offsetMinutes"].Success ? ParseNumber(match.Groups["offsetMinutes"].Value) : 0;
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
        else
        {
            offset = TimeZoneInfo.Local.GetUtcOffset(clock);
        }

        try
        {
            return new DateTimeOffset(clock, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes a moment as "yyyy-MM-ddTHH:mm:ss" at the given offset, with ".fff" when asked, then "Z" or the offset.
    /// </summary>
    /// <param name="instant">The moment to write.</param>
    /// <param name="offset">The offset from UTC to write it at.</param>
    /// <param name="milliseconds">Whether to write the milliseconds.</param>
    /// <returns>The moment as an ISO 8601 date and time.</returns>
    public static string FormatDate(DateTimeOffset instant, TimeSpan offset, bool milliseconds)
    {
        var shifted = instant.ToOffset(offset);
        var format = milliseconds ? "yyyy-MM-dd'T'HH:mm:ss.fff" : "yyyy-MM-dd'T'HH:mm:ss";
        var clock = shifted.ToString(format, CultureInfo.InvariantCulture);
        if (offset == TimeSpan.Zero)
        {
            return clock + "Z";
        }

        var sign = offset < TimeSpan.Zero ? "-" : "+";
        return clock + sign + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
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

    private static int ParseNumber(string digits)
    {
        return int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(@"^(?<year>[0-9]{4})-(?<month>[0-9]{2})-(?<day>[0-9]{2})[T ](?<hour>[0-9]{2}):(?<minute>[0-9]{2})(?::(?<second>[0-9]{2})(?:\.(?<fraction>[0-9]+))?)?(?:(?<zulu>Z)|(?<sign>[+-])(?<offsetHours>[0-9]{2})(?::?(?<offsetMinutes>[0-9]{2}))?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}

/// <summary>
/// The action that pastes a copied Unix time as a date and time in the computer's time zone.
/// </summary>
public sealed class PasteAsDateAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-as-date";

    /// <summary>The action's place among the actions.</summary>
    public int Order => 49;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsDate;
    }

    /// <summary>Gives the action's description, which shows what it pastes.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Pasted(item));
    }

    /// <summary>Tells whether the item is text that is a Unix time.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns><see langword="true"/> when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ConvertTimestamp.ParseUnix(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the item's Unix time as a date and time, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Pasted(item) }, cancellationToken).ConfigureAwait(false);
    }

    private static string Pasted(ClipboardItem item)
    {
        var parsed = ConvertTimestamp.ParseUnix(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item is not a Unix time.");
        var offset = TimeZoneInfo.Local.GetUtcOffset(parsed.Instant);
        return ConvertTimestamp.FormatDate(parsed.Instant, offset, parsed.Milliseconds);
    }
}

/// <summary>
/// The action that pastes a copied ISO 8601 date and time as a Unix time in seconds.
/// </summary>
public sealed class PasteAsUnixTimeAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-as-unix-time";

    /// <summary>The action's place among the actions.</summary>
    public int Order => 50;

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsUnixTime;
    }

    /// <summary>Gives the action's description, which shows what it pastes.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Pasted(item));
    }

    /// <summary>Tells whether the item is text that is an ISO 8601 date and time.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns><see langword="true"/> when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && ConvertTimestamp.ParseDate(item.Snapshot.Text) is not null;
    }

    /// <summary>Pastes the item's date and time as a Unix time, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action uses.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = Pasted(item) }, cancellationToken).ConfigureAwait(false);
    }

    private static string Pasted(ClipboardItem item)
    {
        var instant = ConvertTimestamp.ParseDate(item.Snapshot.Text)
            ?? throw new InvalidOperationException("The item is not an ISO 8601 date and time.");
        return ConvertTimestamp.FormatUnix(instant);
    }
}
