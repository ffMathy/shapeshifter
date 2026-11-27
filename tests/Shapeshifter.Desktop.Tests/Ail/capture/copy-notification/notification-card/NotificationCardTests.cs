// ail-derived-from: NotificationCard@4258d2fd339e9550
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>
/// Derived checks for NotificationCard: the copy notification and the tip it can show,
/// driven through the real app on Avalonia's headless platform.
/// </summary>
public sealed class NotificationCardTests
{
    /// <summary>Scenario: a copy on Linux.</summary>
    [Fact(DisplayName = "a copy on Linux")]
    public Task ACopyOnLinux() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Thursday", null);

        await AppHarness.WaitUntilAsync(
            () => app.Notification is { IsVisible: true },
            "the copy notification to appear");

        var notification = app.Notification;
        Assert.NotNull(notification);

        var count = app.History.Items.Count;
        var expectedTitle = count == 1
            ? "Copied · 1 item in your history"
            : $"Copied · {count} items in your history";

        // One line of text: the title alone, with no app name and no preview on Linux.
        var texts = AppHarness.Texts(notification);
        Assert.Equal(new[] { expectedTitle }, texts);

        AppHarness.SaveScreen(notification, "a-copy-on-linux.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>Scenario: a tip on Linux.</summary>
    [Fact(DisplayName = "a tip on Linux")]
    public Task ATipOnLinux() => AppHarness.RunAsync(async app =>
    {
        const string title = "Your history had it";
        const string message =
            "You copied this a moment ago. Hold Ctrl+V to choose it from your history instead of copying it again.";

        await app.ShowTipAsync(title, message);

        await AppHarness.WaitUntilAsync(
            () => app.Notification is { IsVisible: true },
            "the tip to appear");

        var notification = app.Notification;
        Assert.NotNull(notification);

        // A title over a sentence, in that order.
        var texts = AppHarness.Texts(notification);
        Assert.Equal(new[] { title, message }, texts);

        AppHarness.SaveScreen(notification, "a-tip-on-linux.png");
    }, new AppOptions { Look = Look.Linux });
}
