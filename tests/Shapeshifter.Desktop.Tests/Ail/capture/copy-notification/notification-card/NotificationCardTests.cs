// ail-derived-from: NotificationCard@1a2e2643af033ddc
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>
/// Derived checks for the NotificationCard view: the copy notification as the
/// Linux look shows it, for a new copy and for a tip.
/// </summary>
public sealed class NotificationCardTests
{
    /// <summary>
    /// Scenario: a copy on Linux. When something new is copied, a pill with one
    /// line of text appears. The Linux look shows no icon, no app name and no preview.
    /// </summary>
    [Fact(DisplayName = "a copy on Linux")]
    public Task ACopyOnLinux() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Thursday", null);

        var notification = app.Notification;
        Assert.NotNull(notification);
        Assert.True(notification.IsVisible);

        var texts = AppHarness.Texts(notification);
        Assert.Equal(new[] { "Copied · 1 items in your history" }, texts);

        AppHarness.SaveScreen(notification, "a-copy-on-linux.png");
    }, new AppOptions { Look = Look.Linux, Language = "en" });

    /// <summary>
    /// Scenario: a tip on Linux. When a tip is shown, the same pill shows the
    /// tip's title over its sentence.
    /// </summary>
    [Fact(DisplayName = "a tip on Linux")]
    public Task ATipOnLinux() => AppHarness.RunAsync(async app =>
    {
        const string title = "Your history had it";
        const string message =
            "You copied this a moment ago. Hold Ctrl+V to choose it from your history instead of copying it again.";

        await app.ShowTipAsync(title, message);

        var notification = app.Notification;
        Assert.NotNull(notification);
        Assert.True(notification.IsVisible);

        var texts = AppHarness.Texts(notification);
        Assert.Equal(new[] { title, message }, texts);

        AppHarness.SaveScreen(notification, "a-tip-on-linux.png");
    }, new AppOptions { Look = Look.Linux, Language = "en" });
}
