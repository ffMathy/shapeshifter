// ail-derived-from: ConvertTimestamp@5b542b4488d8b68d
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

public sealed class ConvertTimestampTests
{
    [Fact(DisplayName = "example 1: ParseUnix(\"1795703400\") => 2026-11-26T14:30:00Z, Milliseconds false")]
    public void Example01ParseUnixSeconds()
    {
        var result = ConvertTimestamp.ParseUnix("1795703400");

        Assert.True(result is { } parsed);
        if (result is { } value)
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero), value.Instant);
            Assert.False(value.Milliseconds);
        }
    }

    [Fact(DisplayName = "example 2: ParseUnix(\" 1795703400123 \") => 2026-11-26T14:30:00.123Z, Milliseconds true")]
    public void Example02ParseUnixMilliseconds()
    {
        var result = ConvertTimestamp.ParseUnix(" 1795703400123 ");

        Assert.True(result is { } parsed);
        if (result is { } value)
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 14, 30, 0, 123, TimeSpan.Zero), value.Instant);
            Assert.True(value.Milliseconds);
        }
    }

    [Fact(DisplayName = "example 3: ParseUnix(\"999999999\") => nothing; nine digits")]
    public void Example03ParseUnixNineDigits()
    {
        Assert.Null(ConvertTimestamp.ParseUnix("999999999"));
    }

    [Fact(DisplayName = "example 4: ParseUnix(\"0999999999\") => nothing; before 1000000000")]
    public void Example04ParseUnixBefore2001()
    {
        Assert.Null(ConvertTimestamp.ParseUnix("0999999999"));
    }

    [Fact(DisplayName = "example 5: ParseUnix(\"12345678901\") => nothing; eleven digits")]
    public void Example05ParseUnixElevenDigits()
    {
        Assert.Null(ConvertTimestamp.ParseUnix("12345678901"));
    }

    [Fact(DisplayName = "example 6: ParseUnix(\"1795703400.5\") => nothing")]
    public void Example06ParseUnixFraction()
    {
        Assert.Null(ConvertTimestamp.ParseUnix("1795703400.5"));
    }

    [Fact(DisplayName = "example 7: FormatDate(2026-11-26T14:30:00Z, +01:00, false) => \"2026-11-26T15:30:00+01:00\"")]
    public void Example07FormatDatePositiveOffset()
    {
        var instant = new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-11-26T15:30:00+01:00", ConvertTimestamp.FormatDate(instant, TimeSpan.FromHours(1), false));
    }

    [Fact(DisplayName = "example 8: FormatDate(2026-11-26T14:30:00Z, 0, false) => \"2026-11-26T14:30:00Z\"")]
    public void Example08FormatDateZeroOffset()
    {
        var instant = new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-11-26T14:30:00Z", ConvertTimestamp.FormatDate(instant, TimeSpan.Zero, false));
    }

    [Fact(DisplayName = "example 9: FormatDate(2026-11-26T14:30:00.123Z, -05:00, true) => \"2026-11-26T09:30:00.123-05:00\"")]
    public void Example09FormatDateNegativeOffsetMilliseconds()
    {
        var instant = new DateTimeOffset(2026, 11, 26, 14, 30, 0, 123, TimeSpan.Zero);

        Assert.Equal("2026-11-26T09:30:00.123-05:00", ConvertTimestamp.FormatDate(instant, TimeSpan.FromHours(-5), true));
    }

    [Fact(DisplayName = "example 10: FormatDate(2026-11-26T14:30:00Z, +05:30, true) => \"2026-11-26T20:00:00.000+05:30\"")]
    public void Example10FormatDateHalfHourOffsetMilliseconds()
    {
        var instant = new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-11-26T20:00:00.000+05:30", ConvertTimestamp.FormatDate(instant, new TimeSpan(5, 30, 0), true));
    }

    [Fact(DisplayName = "example 11: ParseDate(\"2026-11-26T14:30:00Z\") => 2026-11-26T14:30:00Z")]
    public void Example11ParseDateUtc()
    {
        var result = ConvertTimestamp.ParseDate("2026-11-26T14:30:00Z");

        Assert.True(result is { } parsed);
        if (result is { } value)
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero), value);
        }
    }

    [Fact(DisplayName = "example 12: ParseDate(\"2026-11-26 15:30+01:00\") => 2026-11-26T14:30:00Z")]
    public void Example12ParseDateSpaceAndOffset()
    {
        var result = ConvertTimestamp.ParseDate("2026-11-26 15:30+01:00");

        Assert.True(result is { } parsed);
        if (result is { } value)
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 14, 30, 0, TimeSpan.Zero), value);
        }
    }

    [Fact(DisplayName = "example 13: ParseDate(\"2026-11-26T09:30:00.75-0500\") => 2026-11-26T14:30:00.75Z")]
    public void Example13ParseDateFractionAndCompactOffset()
    {
        var result = ConvertTimestamp.ParseDate("2026-11-26T09:30:00.75-0500");

        Assert.True(result is { } parsed);
        if (result is { } value)
        {
            Assert.Equal(new DateTimeOffset(2026, 11, 26, 14, 30, 0, 750, TimeSpan.Zero), value);
        }
    }

    [Fact(DisplayName = "example 14: ParseDate(\"2026-11-26\") => nothing; a date alone")]
    public void Example14ParseDateDateAlone()
    {
        Assert.Null(ConvertTimestamp.ParseDate("2026-11-26"));
    }

    [Fact(DisplayName = "example 15: ParseDate(\"14:30\") => nothing")]
    public void Example15ParseDateTimeAlone()
    {
        Assert.Null(ConvertTimestamp.ParseDate("14:30"));
    }

    [Fact(DisplayName = "example 16: ParseDate(\"26/11/2026 14:30\") => nothing; not ISO 8601")]
    public void Example16ParseDateNotIso()
    {
        Assert.Null(ConvertTimestamp.ParseDate("26/11/2026 14:30"));
    }

    [Fact(DisplayName = "example 17: ParseDate(\"2026-13-01T00:00Z\") => nothing; no 13th month")]
    public void Example17ParseDateNoThirteenthMonth()
    {
        Assert.Null(ConvertTimestamp.ParseDate("2026-13-01T00:00Z"));
    }

    [Fact(DisplayName = "example 18: FormatUnix(2026-11-26T14:30:00.75Z) => \"1795703400\"")]
    public void Example18FormatUnixRoundsDown()
    {
        var instant = new DateTimeOffset(2026, 11, 26, 14, 30, 0, 750, TimeSpan.Zero);

        Assert.Equal("1795703400", ConvertTimestamp.FormatUnix(instant));
    }

    [Fact(DisplayName = "example 19: CanExecute of PasteAsDateAction(text \"1795703400\") => true;  of PasteAsUnixTimeAction => false")]
    public void Example19CanExecuteUnixText()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "1795703400" });

        Assert.True(new PasteAsDateAction().CanExecute(item));
        Assert.False(new PasteAsUnixTimeAction().CanExecute(item));
    }

    [Fact(DisplayName = "example 20: CanExecute of PasteAsUnixTimeAction(text \"2026-11-26T14:30:00Z\") => true;  GetDescription => \"Pastes 1795703400.\"")]
    public void Example20CanExecuteAndDescribeDateText()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "2026-11-26T14:30:00Z" });
        var action = new PasteAsUnixTimeAction();

        Assert.True(action.CanExecute(item));
        Assert.Equal("Pastes 1795703400.", action.GetDescription(item));
    }

    [Fact(DisplayName = "example 21: ExecuteAsync of PasteAsUnixTimeAction(text \"2026-11-26T14:30:00Z\") => pastes { Text: \"1795703400\" }; the history is unchanged")]
    public async Task Example21ExecutePastesUnixTime()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "2026-11-26T14:30:00Z" });
        var historyBefore = harness.History.Items.Count;

        await new PasteAsUnixTimeAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("1795703400", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
