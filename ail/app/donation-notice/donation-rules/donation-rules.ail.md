# Donation rules

Whether the donation notice shows, as plain code: the store copy, Sponsorkit
switched off, and the donations Sponsorkit found go in; yes or no comes out.

```ail
node DonationRules : fn   level: system
  why   The notice must never reach a store copy or someone who already gives
        monthly, so the decision is kept apart from the window and checked
        with every kind of donation.   <- DonationNotice
  what
    type Donation = { IsActive: boolean, Frequency: text }
    DonationRules.MonthlyFrequency: text = "monthly"
    DonationRules.IsMonthlyDonor(donations: list of Donation) -> boolean
    DonationRules.ShowsNotice(isStoreCopy: boolean, isDisabled: boolean, donations: list of Donation) -> boolean
    IsMonthlyDonor is true when at least one donation is active and its Frequency is exactly "monthly"; any amount counts.
    ShowsNotice is false for a store copy or when Sponsorkit is switched off, whatever the donations; otherwise it is
      true unless IsMonthlyDonor.
    examples
      IsMonthlyDonor([]) => false
      IsMonthlyDonor([{ IsActive: true, Frequency: "monthly" }]) => true
      IsMonthlyDonor([{ IsActive: false, Frequency: "monthly" }]) => false                     a cancelled monthly donation
      IsMonthlyDonor([{ IsActive: true, Frequency: "yearly" }]) => false
      IsMonthlyDonor([{ IsActive: true, Frequency: "one_time" }]) => false
      IsMonthlyDonor([{ IsActive: true, Frequency: "one_time" }, { IsActive: true, Frequency: "monthly" }]) => true
      IsMonthlyDonor([{ IsActive: true, Frequency: "Monthly" }]) => false                      frequencies compare exactly
      ShowsNotice(false, false, []) => true                                                    signed out, or no donations
      ShowsNotice(false, false, [{ IsActive: true, Frequency: "monthly" }]) => false
      ShowsNotice(false, false, [{ IsActive: true, Frequency: "yearly" }]) => true
      ShowsNotice(true, false, []) => false                                                    a store copy
      ShowsNotice(false, true, []) => false                                                    SPONSORKIT_DISABLE is set
      ShowsNotice(true, true, [{ IsActive: true, Frequency: "monthly" }]) => false
  how
    decide only an active monthly donation removes the notice; a yearly or one-time donation does not
      because the owner asked for a monthly donation to be what removes it (asked by the owner, 2026-11-27)
      rejected counting any active donation, because the owner named monthly ones
    decide any amount counts
      because the owner chose "any active monthly amount" over a minimum (asked by the owner, 2026-11-27)
    decide frequencies compare exactly, against Sponsorkit's lower-case "monthly"
      because Sponsorkit only ever reports "one_time", "monthly" or "yearly"
  sources
    asked by the owner, 2026-11-27: "a monthly donation is needed", "any active monthly amount"
    Sponsorkit .NET SDK 0.1.0: Contribution.IsActive, Contribution.Frequency, Frequencies.Monthly
```
