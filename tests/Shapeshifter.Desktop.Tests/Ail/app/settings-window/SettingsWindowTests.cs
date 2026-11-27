// ail-derived-from: SettingsWindow@4063a5d565dc3ed8
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the settings window's scenarios.</summary>
public sealed class SettingsWindowTests
{
    /// <summary>The settings window shows every setting with its current value.</summary>
    [Fact(DisplayName = "the settings window shows every setting with its current value")]
    public Task TheSettingsWindowShowsEverySettingWithItsCurrentValue() => AppHarness.RunAsync(async app =>
    {
        await Task.Yield();

        var window = await app.OpenSettings();
        var texts = AppHarness.Texts(window).ToList();

        // Every row is shown, in the History and Behaviour groups.
        Assert.Contains("Delay before the history appears", texts);
        Assert.Contains("Items to remember", texts);
        Assert.Contains("Remember history after restart", texts);
        Assert.Contains("Copy notifications", texts);
        Assert.Contains("Tips", texts);
        Assert.Contains("Start at login", texts);
        Assert.Contains("Automatic updates", texts);

        // No "Shortcut" row on Linux.
        Assert.DoesNotContain("Shortcut", texts);

        // The number boxes, in row order: the delay, then items to remember.
        var numbers = AppHarness.Find<NumericUpDown>(window).ToList();
        Assert.Equal(2, numbers.Count);
        Assert.Equal((decimal?)300, numbers[0].Value);
        Assert.Equal((decimal?)50, numbers[1].Value);

        // The switches, in row order: remember history, copy notifications, tips, start at login, automatic updates.
        var switches = AppHarness.Find<ToggleSwitch>(window).ToList();
        Assert.Equal(5, switches.Count);
        Assert.True(switches[0].IsChecked == false, "Remember history after restart should be off");
        Assert.True(switches[1].IsChecked == true, "Copy notifications should be on");
        Assert.True(switches[2].IsChecked == true, "Tips should be on");
        Assert.True(switches[4].IsChecked == true, "Automatic updates should be on");

        AppHarness.SaveScreen(window, "the-settings-window-shows-every-setting-with-its-current-val.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>A copy from an app store leaves out updating.</summary>
    [Fact(DisplayName = "a copy from an app store leaves out updating")]
    public Task ACopyFromAnAppStoreLeavesOutUpdating() => AppHarness.RunAsync(async app =>
    {
        await Task.Yield();

        var window = await app.OpenSettings();
        var texts = AppHarness.Texts(window).ToList();

        Assert.DoesNotContain("Automatic updates", texts);
        Assert.DoesNotContain("Check for updates", texts);

        Assert.Contains("Start at login", texts);
        Assert.Contains("Clear history", texts);
        Assert.Contains("Open log folder", texts);

        AppHarness.SaveScreen(window, "a-copy-from-an-app-store-leaves-out-updating.png");
    }, new AppOptions { Look = Look.Windows, StoreCopy = true });
}
