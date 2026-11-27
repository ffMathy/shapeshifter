// ail-derived-from: NotificationCard@1a2e2643af033ddc
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for NotificationCard: the copy notification and its tip, in the Linux look.</summary>
public sealed class NotificationCardTests
{
    /// <summary>Scenario: a copy on Linux.</summary>
    [Fact(DisplayName = "a copy on Linux")]
    public Task ACopyOnLinux() => AppHarness.RunAsync(async app =>
    {
        // When something new is copied
        await app.CopyAsync("Meeting moved to 3 pm", "Notes");

        await AppHarness.WaitUntilAsync(
            () => app.Notification is { IsVisible: true },
            "the copy notification to appear");

        var notification = app.Notification;
        Assert.NotNull(notification);

        // Then a pill with one line of text appears: only the title, with no icon,
        // no app name and no preview of the item on Linux.
        var expectedTitle = $"Copied · {app.History.Items.Count} items in your history";
        var texts = AppHarness.Texts(notification);
        var only = Assert.Single(texts);
        Assert.Equal(expectedTitle, only);
        Assert.DoesNotContain("Shapeshifter", texts);
        Assert.DoesNotContain("Meeting moved to 3 pm", texts);

        // Colour, rounding and its place at the bottom centre are judged from the screenshot.
        AppHarness.SaveScreen(notification, "a-copy-on-linux.png");
    }, new AppOptions { Look = Look.Linux });

    /// <summary>Scenario: a tip on Linux.</summary>
    [Fact(DisplayName = "a tip on Linux")]
    public Task ATipOnLinux() => AppHarness.RunAsync(async app =>
    {
        const string title = "Your history had it";
        const string message =
            "You copied this a moment ago. Hold Ctrl+V to choose it from your history instead of copying it again.";

        // When a tip is shown
        await app.ShowTipAsync(title, message);

        await AppHarness.WaitUntilAsync(
            () => app.Notification is { IsVisible: true },
            "the tip to appear");

        var notification = app.Notification;
        Assert.NotNull(notification);

        // Then the same pill shows a title over a sentence, in that order, and nothing else.
        var texts = AppHarness.Texts(notification);
        Assert.Equal(new[] { title, message }, texts);

        // Bold title, wrapping and the 420-point width are judged from the screenshot.
        AppHarness.SaveScreen(notification, "a-tip-on-linux.png");
    }, new AppOptions { Look = Look.Linux });
}
