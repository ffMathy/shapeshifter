// ail-derived-from: SettingsWindow@97ca44a90433c610
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the SettingsWindow scenarios, driven on Avalonia's headless platform.</summary>
public sealed class SettingsWindowTests
{
    /// <summary>The settings window shows every setting with its current value.</summary>
    [Fact(DisplayName = "the settings window shows every setting with its current value")]
    public Task TheSettingsWindowShowsEverySettingWithItsCurrentValue() => AppHarness.RunAsync(async app =>
    {
        var window = await app.OpenSettings();

        var delay = ControlInRow<NumericUpDown>(window, "Delay before the history appears");
        Assert.Equal(300m, delay.Value);

        var items = ControlInRow<NumericUpDown>(window, "Items to remember");
        Assert.Equal(50m, items.Value);

        var remember = ControlInRow<ToggleSwitch>(window, "Remember history after restart");
        Assert.False(remember.IsChecked == true, "\"Remember history after restart\" should be off");

        var notifications = ControlInRow<ToggleSwitch>(window, "Copy notifications");
        Assert.True(notifications.IsChecked == true, "\"Copy notifications\" should be on");

        var tips = ControlInRow<ToggleSwitch>(window, "Tips");
        Assert.True(tips.IsChecked == true, "\"Tips\" should be on");

        // Start at login is shown "as it is": its row and switch are there, whatever their state.
        _ = ControlInRow<ToggleSwitch>(window, "Start at login");

        var updates = ControlInRow<ToggleSwitch>(window, "Automatic updates");
        Assert.True(updates.IsChecked == true, "\"Automatic updates\" should be on");

        var texts = AppHarness.Texts(window).ToList();
        Assert.DoesNotContain("Shortcut", texts);

        AppHarness.SaveScreen(window, "the-settings-window-shows-every-setting-with-its-current-val.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>A copy from an app store leaves out updating.</summary>
    [Fact(DisplayName = "a copy from an app store leaves out updating")]
    public Task ACopyFromAnAppStoreLeavesOutUpdating() => AppHarness.RunAsync(async app =>
    {
        var window = await app.OpenSettings();

        var texts = AppHarness.Texts(window).ToList();
        Assert.DoesNotContain("Automatic updates", texts);
        Assert.DoesNotContain("Check for updates", texts);
        Assert.Empty(ButtonsNamed(window, "Check for updates"));

        _ = ControlInRow<ToggleSwitch>(window, "Start at login");
        Assert.Single(ButtonsNamed(window, "Clear history"));
        Assert.Single(ButtonsNamed(window, "Open log folder"));

        AppHarness.SaveScreen(window, "a-copy-from-an-app-store-leaves-out-updating.png");
    }, new AppOptions { Look = Look.Windows, StoreCopy = true });

    private static T ControlInRow<T>(Window window, string title) where T : Control
    {
        var titles = AppHarness.Find<TextBlock>(window).Where(t => t.Text == title).ToList();
        Assert.True(titles.Count == 1, $"Expected exactly one visible \"{title}\" row title, found {titles.Count}.");

        foreach (var ancestor in titles[0].GetVisualAncestors())
        {
            var controls = ancestor.GetVisualDescendants()
                .OfType<T>()
                .Where(c => c.IsEffectivelyVisible)
                .ToList();
            if (controls.Count == 1)
            {
                return controls[0];
            }

            if (controls.Count > 1)
            {
                break;
            }
        }

        Assert.Fail($"No {typeof(T).Name} belongs to the \"{title}\" row.");
        return null!;
    }

    private static List<Button> ButtonsNamed(Window window, string name) =>
        AppHarness.Find<Button>(window)
            .Where(b => b.Content as string == name
                || b.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == name))
            .ToList();
}
