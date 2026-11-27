# The donation card

The notice's window: the copy notification's card, grown to a title, a
sentence and three buttons, in each system's own notification style.

```ail
node DonationCard : view   level: experience
  why   An ask for money must read as part of the desktop, calm and easy to
        dismiss, not as an alarm.   <- DonationNotice
  what
    Scenario: the notice on Linux
      When the notice is shown on Linux
      Then a dark rounded card at the bottom centre shows "Support Shapeshifter" in bold, the message wrapped below it, and the buttons "Donate monthly", "I already donate" and "Not now" in a row under the message
    Scenario: the notice on Windows
      When the notice is shown on Windows, in dark
      Then a Windows notification card at the bottom right shows the app icon and "Shapeshifter", "Support Shapeshifter", the message, and the three buttons side by side, "Donate monthly" in the accent colour
    Scenario: the notice on a Mac
      When the notice is shown on a Mac
      Then a frosted banner at the top right shows the app icon, "Support Shapeshifter", the message and the three buttons under it
    Scenario: the notice in German
      When the notice is shown on Linux with German as the display language
      Then the title, the message and the three buttons are in German, and the message wraps rather than being cut off
  look
    every look      the notification card of NotificationCard in its tip form (a title over a wrapped sentence), with a row
                    of three buttons under the sentence, in the order "Donate monthly", "I already donate", "Not now";
                    a window of its own: no frame, always on top, never focused, not in the taskbar, sized to its
                    content, placed as NotificationCard; it does not go away when pointed at, only when a button closes it
    Linux           dark card #2E2E33 with corner radius 12, 20 points of padding at the sides and 14 at top and bottom;
                    title 14-point bold white, message 14-point white at 87 % opacity wrapped at 420 points; buttons
                    right-aligned 8 points apart and 12 points under the message, 13-point semibold, 32 tall with corner
                    radius 6: "Donate monthly" filled with the accent and white text, the other two white text on #1AFFFFFF
    Linux, no compositor  the card fills its window, with square corners and no shadow
    Windows         the Fluent notification card of NotificationCard (330 wide body, 8 corner radius): app icon and
                    "Shapeshifter" at 12 points, title 14-point semibold, message 13 points in up to five lines; the
                    three buttons share the width equally, 32 tall, 4 points apart and 12 points under the message:
                    "Donate monthly" filled with the accent, the others Fluent's standard buttons
    Mac             the Refraction banner of NotificationCard (340 wide, corner radius 22): app icon at the left, title
                    13-point semibold, message 12 points wrapped; the buttons under the message, right-aligned, 6
                    points apart, 26 tall with corner radius 13: "Donate monthly" filled with the accent, the others
                    on #0F000000
    right to left   the card mirrors for Arabic and Persian
  copy
    app     "Shapeshifter"            next to the icon, where the look shows it
    title   "Support Shapeshifter"
    message "Shapeshifter is free, and donating is optional. A monthly donation supports the project, keeps it running, and removes this message when Shapeshifter starts."
    donate  "Donate monthly"
    already "I already donate"
    later   "Not now"
  uses      NotificationCard, Refraction
  sources
    asked by the owner, 2026-11-27 (see DonationNotice)
    src/Shapeshifter.Desktop/Views/DonationWindow.axaml; the donation styles in Styles/Linux.axaml, Windows.axaml and MacOS.axaml
```
