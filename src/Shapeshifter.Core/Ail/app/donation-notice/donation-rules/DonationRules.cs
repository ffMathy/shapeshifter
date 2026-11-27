using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// One donation as Sponsorkit reports it: whether it is still active and how often it recurs.
/// </summary>
/// <param name="IsActive">Whether the donation is still active.</param>
/// <param name="Frequency">How often the donation recurs, as Sponsorkit spells it: "one_time", "monthly" or "yearly".</param>
public sealed record Donation(bool IsActive, string Frequency);

/// <summary>
/// Decides whether the donation notice shows, kept apart from its window so it can be checked with every kind of donation.
/// </summary>
public static class DonationRules
{
    /// <summary>
    /// The frequency Sponsorkit reports for a monthly donation, the only kind that removes the notice.
    /// </summary>
    public const string MonthlyFrequency = "monthly";

    /// <summary>
    /// Tells whether at least one donation is active and monthly, of any amount.
    /// </summary>
    /// <param name="donations">The donations Sponsorkit reports.</param>
    /// <returns>True when an active donation has exactly the monthly frequency.</returns>
    public static bool IsMonthlyDonor(IReadOnlyList<Donation> donations)
    {
        ArgumentNullException.ThrowIfNull(donations);
        for (var i = 0; i < donations.Count; i++)
        {
            var donation = donations[i];
            if (donation.IsActive && string.Equals(donation.Frequency, MonthlyFrequency, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether the donation notice shows: never for a store copy or when Sponsorkit is switched off, otherwise unless the user gives monthly.
    /// </summary>
    /// <param name="isStoreCopy">Whether this copy was installed from an app store.</param>
    /// <param name="isDisabled">Whether Sponsorkit is switched off.</param>
    /// <param name="donations">The donations Sponsorkit reports.</param>
    /// <returns>True when the notice shows.</returns>
    public static bool ShowsNotice(bool isStoreCopy, bool isDisabled, IReadOnlyList<Donation> donations)
    {
        ArgumentNullException.ThrowIfNull(donations);
        if (isStoreCopy || isDisabled)
        {
            return false;
        }

        return !IsMonthlyDonor(donations);
    }
}
