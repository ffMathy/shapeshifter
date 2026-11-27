// ail-derived-from: SettingsWindow@d9d6acfe1ad7379e
using Avalonia.Controls;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the settings window's scenarios.</summary>
public sealed class SettingsWindowTests
{
    /// <summary>Scenario: the settings window shows every setting with its current value.</summary>
    [Fact(DisplayName = "the settings window shows every setting with its current value")]
    public Task TheSettingsWindowShowsEverySettingWithItsCurrentValue() => AppHarness.RunAsync(async app =>
    {
        var window = await app.OpenSettings();
        await AppHarness.WaitUntilAsync(() => app.SettingsWindow is { IsVisible: true }, "the settings window to open");

        var texts = AppHarness.Texts(window);
        Assert.Contains("Delay before the history appears", texts);
        Assert.Contains("Items to remember", texts);
        Assert.Contains("Remember history after restart", texts);
        Assert.Contains("Copy notifications", texts);
        Assert.Contains("Tips", texts);
        Assert.Contains("Start at login", texts);
        Assert.Contains("Automatic updates", texts);

        List<NumericUpDown> numbers = [.. AppHarness.Find<NumericUpDown>(window)];
        Assert.Equal(2, numbers.Count);
        Assert.Equal(300m, numbers[0].Value);
        Assert.Equal(50m, numbers[1].Value);

        // In the order of the rows: remember history, copy notifications, tips, start at login, automatic updates.
        List<ToggleSwitch> switches = [.. AppHarness.Find<ToggleSwitch>(window)];
        Assert.Equal(5, switches.Count);
        Assert.False(switches[0].IsChecked);
        Assert.True(switches[1].IsChecked);
        Assert.True(switches[2].IsChecked);
        Assert.True(switches[4].IsChecked);

        Assert.DoesNotContain("Shortcut", texts);

        AppHarness.SaveScreen(window, "the-settings-window-shows-every-setting-with-its-current-val.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>Scenario: a copy from an app store leaves out updating.</summary>
    [Fact(DisplayName = "a copy from an app store leaves out updating")]
    public Task ACopyFromAnAppStoreLeavesOutUpdating() => AppHarness.RunAsync(async app =>
    {
        var window = await app.OpenSettings();
        await AppHarness.WaitUntilAsync(() => app.SettingsWindow is { IsVisible: true }, "the settings window to open");

        var texts = AppHarness.Texts(window);
        Assert.DoesNotContain("Automatic updates", texts);
        Assert.DoesNotContain("Check for updates", texts);

        Assert.Contains("Start at login", texts);
        Assert.Contains("Clear history", texts);
        Assert.Contains("Open log folder", texts);

        AppHarness.SaveScreen(window, "a-copy-from-an-app-store-leaves-out-updating.png");
    }, new AppOptions { Look = Look.Windows, StoreCopy = true });
}
