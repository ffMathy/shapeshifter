# The donation notice

When a free copy of Shapeshifter starts, a small notice asks for an optional
monthly donation through Sponsorkit (GitHub Sponsors or Open Collective). A
monthly donor never sees it, and neither does a copy bought from an app store.

```
donation-notice.ail.md                       DonationNotice (this file)
donation-card/donation-card.ail.md           DonationCard: the notice's window, per look
donation-rules/donation-rules.ail.md         DonationRules: whether it shows
```

```ail
node DonationNotice : flow   level: experience
  why   Shapeshifter is free from GitHub, and only donations keep it going; a
        short, honest ask at each start reaches the people who use it, while
        those who already give monthly, or bought it from a store, are left
        alone.   <- App
  what
    the notice asks once per start, a few seconds after Shapeshifter is ready, and stays until the user answers it;
      it never takes the keyboard focus from the app in front
    whether to ask is the donation rule: never in a copy from an app store (the Microsoft Store or the Mac App Store),
      never when SPONSORKIT_DISABLE is set, and never for someone with an active monthly donation of any amount to
      GitHub Sponsors "ffMathy" or Open Collective "shapeshifterclipboard"; everyone else is asked, signed in or not
    a copy from an app store does not ask Sponsorkit anything at all
    when Sponsorkit cannot be reached or answers with an error, nothing shows and the reason is logged: a donor must
      never be asked because of an outage
    "Donate monthly" opens Sponsorkit's checkout for a monthly donation in the browser, signing in first when needed;
      once Sponsorkit reports an active monthly donation, the notice closes
    "I already donate" signs in through Sponsorkit in the browser; when a monthly donation is found the notice closes,
      otherwise it stays
    "Not now" closes the notice until the next start
    a sign-in or checkout that is cancelled, times out or fails leaves the notice as it was
    Scenario: a copy without a monthly donation asks for one when it starts
      Given Shapeshifter is not from an app store and the user has no monthly donation
      When Shapeshifter starts
      Then a few seconds later the notice appears where the system's notifications appear
      And it reads "Support Shapeshifter" over "Shapeshifter is free, and donating is optional. A monthly donation supports the project, keeps it running, and removes this message when Shapeshifter starts."
      And it offers "Donate monthly", "I already donate" and "Not now"
      And it stays on screen
    Scenario: Not now closes the notice
      Given the notice is on screen
      When the user clicks "Not now"
      Then the notice is gone
    Scenario: donating monthly closes the notice
      Given the notice is on screen and the user has no monthly donation
      When the user clicks "Donate monthly" and completes a monthly donation
      Then the notice is gone
    Scenario: I already donate keeps the notice when no monthly donation is found
      Given the notice is on screen and the user has no monthly donation
      When the user clicks "I already donate" and signs in
      Then the notice is still on screen
    Scenario: a monthly donor is not asked
      Given the user has an active monthly donation
      When Shapeshifter starts
      Then no notice appears
    Scenario: a copy from an app store never asks
      Given Shapeshifter was installed from the Microsoft Store
      When Shapeshifter starts
      Then no notice appears
  how
    decide ask at every start, rather than every few starts or days
      because the owner asked for the notice "when the application starts", removed only by donating (asked by the owner, 2026-11-27)
      rejected Sponsorkit's nag counters (every N runs or days), because the owner wants it at each start
    decide no setting turns the notice off
      because the owner made a monthly donation the way to remove it (asked by the owner, 2026-11-27)
    decide copies from an app store never ask, the Mac App Store's included
      because those copies were bought, which already supports the project, and Apple does not allow App Store apps to
      ask for donations elsewhere (the owner chose "every store copy", 2026-11-27)
    decide the donations counted are to GitHub Sponsors "ffMathy" and Open Collective "shapeshifterclipboard"
      because those are Shapeshifter's funding accounts in .github/FUNDING.yml (the owner chose GitHub and Open Collective, 2026-11-27)
    decide the notice waits a few seconds after start and never takes the focus
      because Shapeshifter starts at login, when the user is busy starting other things
    decide when the check fails, nothing shows
      because asking someone who already donates, just because the network is down, would be worse than not asking once
    decide SPONSORKIT_DISABLE turns it off
      because it is Sponsorkit's own switch for CI and packagers, and the end-to-end checks of everything else use it
    decide the copy notification may briefly show over the notice
      because both sit where the system's notifications do, and the copy notification is gone within 1.5 s
  copy
    donation.title     "Support Shapeshifter"
    donation.message   "Shapeshifter is free, and donating is optional. A monthly donation supports the project, keeps it running, and removes this message when Shapeshifter starts."
    donation.donate    "Donate monthly"
    donation.already   "I already donate"
    donation.later     "Not now"
  uses      DonationRules
  children  DonationCard, DonationRules
  sources
    asked by the owner, 2026-11-27: at start, ask to donate, optional, supports and keeps the project running, removes
      the message; a monthly donation is needed; not in a copy bought from the Microsoft Store
    the owner's answers, 2026-11-27: every store copy; GitHub and Open Collective; any active monthly amount
    .github/FUNDING.yml, its GitHub Sponsors and Open Collective accounts
    Sponsorkit .NET SDK 0.1.0: SponsorkitClient.GetFinancialContributionAsync, OpenCheckoutAsync, SignInAsync, IsDisabled, SPONSORKIT_MOCK
```
