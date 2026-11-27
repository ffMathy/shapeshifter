using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Reads and writes moments as Unix times and as ISO 8601 dates and times.
/// </summary>
public static partial class ConvertTimestamp
{
    /// <summary>
    /// Reads a Unix time of 10 digits (seconds) or 13 digits (milliseconds), from 2001 on.
    /// </summary>
    /// <param name="text">The text to read; white space at its start and end is ignored.</param>
    /// <returns>The moment and whether it was given in milliseconds, or null when the text is not such a Unix time.</returns>
    public static (DateTimeOffset Instant, bool Milliseconds)? ParseUnix(string? text)
    {
        if (text is null)
        {
            return null;
        }

        string trimmed = text.Trim();
        if (trimmed.Length is not (10 or 13))
        {
            return null;
        }

        foreach (char c in trimmed)
        {
            if (c is < '0' or > '9')
            {
                return null;
            }
        }

        long value = long.Parse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture);
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
    /// Reads an ISO 8601 date with a time; without Z or an offset it is read as a time on the computer's clock.
    /// </summary>
    /// <param name="text">The text to read; white space at its start and end is ignored.</param>
    /// <returns>The moment in UTC, or null when the text is not an ISO 8601 date and time.</returns>
    public static DateTimeOffset? ParseDate(string? text)
    {
        if (text is null)
        {
            return null;
        }

        Match match = DatePattern().Match(text.Trim());
        if (!match.Success)
        {
            return null;
        }

        int year = ParseInt(match.Groups["year"].Value);
        int month = ParseInt(match.Groups["month"].Value);
        int day = ParseInt(match.Groups["day"].Value);
        int hour = ParseInt(match.Groups["hour"].Value);
        int minute = ParseInt(match.Groups["minute"].Value);
        int second = match.Groups["second"].Success ? ParseInt(match.Groups["second"].Value) : 0;
        long fractionTicks = 0;
        if (match.Groups["fraction"].Success)
        {
            string fraction = match.Groups["fraction"].Value;
            fraction = fraction.Length > 7 ? fraction[..7] : fraction.PadRight(7, '0');
            fractionTicks = long.Parse(fraction, NumberStyles.None, CultureInfo.InvariantCulture);
        }

        try
        {
            DateTime clock = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified).AddTicks(fractionTicks);
            TimeSpan offset;
            if (match.Groups["zone"].Success)
            {
                string zone = match.Groups["zone"].Value;
                if (zone == "Z")
                {
                    offset = TimeSpan.Zero;
                }
                else
                {
                    int offsetHours = ParseInt(match.Groups["offsetHours"].Value);
                    int offsetMinutes = match.Groups["offsetMinutes"].Success ? ParseInt(match.Groups["offsetMinutes"].Value) : 0;
                    if (offsetMinutes > 59)
                    {
                        return null;
                    }

                    offset = new TimeSpan(offsetHours, offsetMinutes, 0);
                    if (zone[0] == '-')
                    {
                        offset = -offset;
                    }
                }
            }
            else
            {
                offset = TimeZoneInfo.Local.GetUtcOffset(clock);
            }

            return new DateTimeOffset(clock, offset).ToUniversalTime();
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
    /// Writes a moment as an ISO 8601 date and time at the given offset.
    /// </summary>
    /// <param name="instant">The moment to write.</param>
    /// <param name="offset">The offset from UTC to write it at.</param>
    /// <param name="milliseconds">Whether to write the milliseconds.</param>
    /// <returns>"yyyy-MM-ddTHH:mm:ss", then ".fff" when asked, then "Z" or the offset as "+HH:mm" or "-HH:mm".</returns>
    public static string FormatDate(DateTimeOffset instant, TimeSpan offset, bool milliseconds)
    {
        DateTimeOffset shifted = instant.ToOffset(offset);
        string format = milliseconds ? "yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff" : "yyyy'-'MM'-'dd'T'HH':'mm':'ss";
        string clock = shifted.ToString(format, CultureInfo.InvariantCulture);
        if (offset == TimeSpan.Zero)
        {
            return clock + "Z";
        }

        string sign = offset < TimeSpan.Zero ? "-" : "+";
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

    private static int ParseInt(string digits)
    {
        return int.Parse(digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    [GeneratedRegex(
        "^(?<year>[0-9]{4})-(?<month>[0-9]{2})-(?<day>[0-9]{2})[T ](?<hour>[0-9]{2}):(?<minute>[0-9]{2})(?::(?<second>[0-9]{2})(?:\\.(?<fraction>[0-9]+))?)?(?<zone>Z|[+-](?<offsetHours>[0-9]{2})(?::?(?<offsetMinutes>[0-9]{2}))?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DatePattern();
}

/// <summary>
/// An action that pastes a copied Unix time as a date and time in the computer's time zone.
/// </summary>
public sealed class PasteAsDateAction : IClipboardAction
{
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-as-date";

    /// <summary>
    /// Where the action stands among the actions.
    /// </summary>
    public int Order => 49;

    /// <summary>
    /// The action's title, shown in the actions.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsDate;
    }

    /// <summary>
    /// The action's description, showing what it pastes.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Pasted(item) ?? string.Empty);
    }

    /// <summary>
    /// Whether the item is text holding a Unix time.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Pasted(item) is not null;
    }

    /// <summary>
    /// Pastes the item's Unix time as a date and time, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action may use.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text was pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        string? text = Pasted(item);
        if (text is null)
        {
            return Task.CompletedTask;
        }

        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = text }, cancellationToken);
    }

    private static string? Pasted(ClipboardItem item)
    {
        if (item.Kind != ClipboardContentKind.Text)
        {
            return null;
        }

        (DateTimeOffset Instant, bool Milliseconds)? parsed = ConvertTimestamp.ParseUnix(item.Snapshot.Text);
        if (parsed is not { } value)
        {
            return null;
        }

        TimeSpan offset = TimeZoneInfo.Local.GetUtcOffset(value.Instant);
        return ConvertTimestamp.FormatDate(value.Instant, offset, value.Milliseconds);
    }
}

/// <summary>
/// An action that pastes a copied ISO 8601 date and time as a Unix time in seconds.
/// </summary>
public sealed class PasteAsUnixTimeAction : IClipboardAction
{
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-as-unix-time";

    /// <summary>
    /// Where the action stands among the actions.
    /// </summary>
    public int Order => 50;

    /// <summary>
    /// The action's title, shown in the actions.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsUnixTime;
    }

    /// <summary>
    /// The action's description, showing what it pastes.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionTimestampDescription, Pasted(item) ?? string.Empty);
    }

    /// <summary>
    /// Whether the item is text holding an ISO 8601 date and time.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Pasted(item) is not null;
    }

    /// <summary>
    /// Pastes the item's date and time as a Unix time, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The services the action may use.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text was pasted.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        string? text = Pasted(item);
        if (text is null)
        {
            return Task.CompletedTask;
        }

        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = text }, cancellationToken);
    }

    private static string? Pasted(ClipboardItem item)
    {
        if (item.Kind != ClipboardContentKind.Text)
        {
            return null;
        }

        DateTimeOffset? parsed = ConvertTimestamp.ParseDate(item.Snapshot.Text);
        if (parsed is not { } instant)
        {
            return null;
        }

        return ConvertTimestamp.FormatUnix(instant);
    }
}
