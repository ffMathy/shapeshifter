# Convert a timestamp

A copied Unix time, such as 1795703400, can be pasted as a date and time, and
a copied date and time, such as 2026-11-26T14:30:00Z, as a Unix time.

```ail
node ConvertTimestamp : fn   level: system
  why   Logs, databases and APIs write moments as Unix times or as ISO 8601
        dates, and reading one, or writing it into a query, meant converting
        it by hand or on a website.   <- Actions
  what
    ConvertTimestamp.ParseUnix(text: text or nothing) -> { Instant: time, Milliseconds: boolean } or nothing
      the text, without the white space at its start and end, must be only digits: 10 of them, seconds since 1970-01-01T00:00:00Z from 1000000000 (2001) on, or 13 of them, milliseconds from 1000000000000 on; anything else gives nothing
    ConvertTimestamp.ParseDate(text: text or nothing) -> time or nothing
      the text, without the white space at its start and end, must be an ISO 8601 date and time: yyyy-MM-dd, then T or a space, then HH:mm, maybe :ss, maybe a fraction of a second, and maybe Z or an offset such as +01:00, -0500 or +01
      without Z or an offset, it is a time on the computer's clock, in its time zone at that moment
      a date alone, a time alone, and any other text give nothing
    ConvertTimestamp.FormatDate(instant: time, offset: duration, milliseconds: boolean) -> text
      "yyyy-MM-ddTHH:mm:ss" at that offset, then ".fff" when milliseconds is true, then "Z" when the offset is zero, else the offset as "+HH:mm" or "-HH:mm"
    ConvertTimestamp.FormatUnix(instant: time) -> text
      the whole seconds since 1970-01-01T00:00:00Z, rounded down, as digits
    PasteAsDateAction is an IClipboardAction with Id "paste-as-date" and Order 49:
      CanExecute(item) is true when item.Kind is Text and ParseUnix(item.Snapshot.Text) gives a time
      what it pastes is FormatDate(that time, the offset of the computer's time zone at that time, whether it was given in milliseconds)
      GetTitle(item) is Strings.ActionPasteAsDate, "Paste as date and time"
    PasteAsUnixTimeAction is an IClipboardAction with Id "paste-as-unix-time" and Order 50:
      CanExecute(item) is true when item.Kind is Text and ParseDate(item.Snapshot.Text) gives a time
      what it pastes is FormatUnix(that time)
      GetTitle(item) is Strings.ActionPasteAsUnixTime, "Paste as Unix time"
    both: GetDescription(item) is Localizer.Format(Strings.ActionTimestampDescription, what it pastes), "Pastes {0}."; ExecuteAsync pastes ClipboardSnapshot { Text: what it pastes } with context.WriteAndPasteAsync; nothing is added to the history
    examples
      ParseUnix("1795703400") => 2026-11-26T14:30:00Z, Milliseconds false
      ParseUnix(" 1795703400123 ") => 2026-11-26T14:30:00.123Z, Milliseconds true
      ParseUnix("999999999") => nothing; nine digits
      ParseUnix("0999999999") => nothing; before 1000000000
      ParseUnix("12345678901") => nothing; eleven digits
      ParseUnix("1795703400.5") => nothing
      FormatDate(2026-11-26T14:30:00Z, +01:00, false) => "2026-11-26T15:30:00+01:00"
      FormatDate(2026-11-26T14:30:00Z, 0, false) => "2026-11-26T14:30:00Z"
      FormatDate(2026-11-26T14:30:00.123Z, -05:00, true) => "2026-11-26T09:30:00.123-05:00"
      FormatDate(2026-11-26T14:30:00Z, +05:30, true) => "2026-11-26T20:00:00.000+05:30"
      ParseDate("2026-11-26T14:30:00Z") => 2026-11-26T14:30:00Z
      ParseDate("2026-11-26 15:30+01:00") => 2026-11-26T14:30:00Z
      ParseDate("2026-11-26T09:30:00.75-0500") => 2026-11-26T14:30:00.75Z
      ParseDate("2026-11-26") => nothing; a date alone
      ParseDate("14:30") => nothing
      ParseDate("26/11/2026 14:30") => nothing; not ISO 8601
      ParseDate("2026-13-01T00:00Z") => nothing; no 13th month
      FormatUnix(2026-11-26T14:30:00.75Z) => "1795703400"
      CanExecute of PasteAsDateAction(text "1795703400") => true;  of PasteAsUnixTimeAction => false
      CanExecute of PasteAsUnixTimeAction(text "2026-11-26T14:30:00Z") => true;  GetDescription => "Pastes 1795703400."
      ExecuteAsync of PasteAsUnixTimeAction(text "2026-11-26T14:30:00Z") => pastes { Text: "1795703400" }; the history is unchanged
  how
    decide 10 digits for seconds and 13 for milliseconds, from 2001 on
      because shorter or longer numbers are mostly not times (an order number, a phone number), and every Unix time since September 2001 has 10 digits
    decide the date pasted is in the computer's time zone, with its offset
      because the user reads it as their own time, and the offset keeps it exact wherever it is pasted
    decide milliseconds in, milliseconds out; seconds out of a date are rounded down
      because the result should be as precise as what was copied, and Unix seconds are whole
    decide ISO 8601 only, a date with a time, and a date and time without an offset read as local time
      because other notations (26/11 or 11/26) cannot be read without guessing the order, and a date alone has no moment
    decide the description shows the result
      because the user sees what will be pasted before letting go of Ctrl (#62)
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R7 Convert timestamp"
```
