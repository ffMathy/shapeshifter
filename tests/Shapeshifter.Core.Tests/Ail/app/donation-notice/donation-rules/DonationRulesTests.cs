// ail-derived-from: DonationRules@dc4b43ffc6a5649e
using Shapeshifter.Core.Ail;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>
/// Derived checks for DonationRules: which donations count as a monthly donation,
/// and when the donation notice shows.
/// </summary>
public sealed class DonationRulesTests
{
    /// <summary>Example 1: no donations is not a monthly donor.</summary>
    [Fact(DisplayName = "example 1: IsMonthlyDonor([]) => false")]
    public void Example01()
    {
        Assert.False(DonationRules.IsMonthlyDonor([]));
    }

    /// <summary>Example 2: an active monthly donation is a monthly donor.</summary>
    [Fact(DisplayName = "example 2: IsMonthlyDonor([{ IsActive: true, Frequency: \"monthly\" }]) => true")]
    public void Example02()
    {
        Assert.True(DonationRules.IsMonthlyDonor([new Donation(IsActive: true, Frequency: "monthly")]));
    }

    /// <summary>Example 3: a cancelled monthly donation is not a monthly donor.</summary>
    [Fact(DisplayName = "example 3: IsMonthlyDonor([{ IsActive: false, Frequency: \"monthly\" }]) => false")]
    public void Example03()
    {
        Assert.False(DonationRules.IsMonthlyDonor([new Donation(IsActive: false, Frequency: "monthly")]));
    }

    /// <summary>Example 4: an active yearly donation is not a monthly donor.</summary>
    [Fact(DisplayName = "example 4: IsMonthlyDonor([{ IsActive: true, Frequency: \"yearly\" }]) => false")]
    public void Example04()
    {
        Assert.False(DonationRules.IsMonthlyDonor([new Donation(IsActive: true, Frequency: "yearly")]));
    }

    /// <summary>Example 5: an active one-time donation is not a monthly donor.</summary>
    [Fact(DisplayName = "example 5: IsMonthlyDonor([{ IsActive: true, Frequency: \"one_time\" }]) => false")]
    public void Example05()
    {
        Assert.False(DonationRules.IsMonthlyDonor([new Donation(IsActive: true, Frequency: "one_time")]));
    }

    /// <summary>Example 6: one active monthly donation among others is enough.</summary>
    [Fact(DisplayName = "example 6: IsMonthlyDonor([{ IsActive: true, Frequency: \"one_time\" }, { IsActive: true, Frequency: \"monthly\" }]) => true")]
    public void Example06()
    {
        Assert.True(DonationRules.IsMonthlyDonor(
        [
            new Donation(IsActive: true, Frequency: "one_time"),
            new Donation(IsActive: true, Frequency: "monthly"),
        ]));
    }

    /// <summary>Example 7: frequencies compare exactly.</summary>
    [Fact(DisplayName = "example 7: IsMonthlyDonor([{ IsActive: true, Frequency: \"Monthly\" }]) => false")]
    public void Example07()
    {
        Assert.False(DonationRules.IsMonthlyDonor([new Donation(IsActive: true, Frequency: "Monthly")]));
    }

    /// <summary>Example 8: signed out, or no donations, shows the notice.</summary>
    [Fact(DisplayName = "example 8: ShowsNotice(false, false, []) => true")]
    public void Example08()
    {
        Assert.True(DonationRules.ShowsNotice(false, false, []));
    }

    /// <summary>Example 9: an active monthly donation hides the notice.</summary>
    [Fact(DisplayName = "example 9: ShowsNotice(false, false, [{ IsActive: true, Frequency: \"monthly\" }]) => false")]
    public void Example09()
    {
        Assert.False(DonationRules.ShowsNotice(false, false, [new Donation(IsActive: true, Frequency: "monthly")]));
    }

    /// <summary>Example 10: an active yearly donation still shows the notice.</summary>
    [Fact(DisplayName = "example 10: ShowsNotice(false, false, [{ IsActive: true, Frequency: \"yearly\" }]) => true")]
    public void Example10()
    {
        Assert.True(DonationRules.ShowsNotice(false, false, [new Donation(IsActive: true, Frequency: "yearly")]));
    }

    /// <summary>Example 11: a store copy never shows the notice.</summary>
    [Fact(DisplayName = "example 11: ShowsNotice(true, false, []) => false")]
    public void Example11()
    {
        Assert.False(DonationRules.ShowsNotice(true, false, []));
    }

    /// <summary>Example 12: with SPONSORKIT_DISABLE set, the notice never shows.</summary>
    [Fact(DisplayName = "example 12: ShowsNotice(false, true, []) => false")]
    public void Example12()
    {
        Assert.False(DonationRules.ShowsNotice(false, true, []));
    }

    /// <summary>Example 13: a disabled store copy with a monthly donation does not show the notice.</summary>
    [Fact(DisplayName = "example 13: ShowsNotice(true, true, [{ IsActive: true, Frequency: \"monthly\" }]) => false")]
    public void Example13()
    {
        Assert.False(DonationRules.ShowsNotice(true, true, [new Donation(IsActive: true, Frequency: "monthly")]));
    }
}
