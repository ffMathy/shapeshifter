// ail-derived-from: DonationCard@22bffbf5341a61db
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the scenarios of the DonationCard view.</summary>
public sealed class DonationCardTests
{
    private const string AppName = "Shapeshifter";
    private const string Title = "Support Shapeshifter";
    private const string Message = "Shapeshifter is free, and donating is optional. A monthly donation supports the project, keeps it running, and removes this message when Shapeshifter starts.";
    private const string Donate = "Donate monthly";
    private const string Already = "I already donate";
    private const string Later = "Not now";

    /// <summary>The notice on Linux: a card with the bold title, the message and a row of three buttons under it.</summary>
    [Fact(DisplayName = "the notice on Linux")]
    public Task TheNoticeOnLinux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This scenario applies on Linux only.");
        return AppHarness.RunAsync(async app =>
        {
            var window = await ShowNoticeAsync(app);

            var texts = AppHarness.Texts(window).ToList();
            var titleIndex = texts.IndexOf(Title);
            var messageIndex = texts.IndexOf(Message);
            Assert.True(titleIndex >= 0, "The title is shown.");
            Assert.True(messageIndex > titleIndex, "The message is shown below the title.");

            var title = FindText(window, Title);
            Assert.True(title.FontWeight >= FontWeight.Bold, "The title is bold.");

            var message = FindText(window, Message);
            Assert.NotEqual(TextWrapping.NoWrap, message.TextWrapping);

            var buttons = ButtonsInOrder(window);
            AssertInARow(buttons, window);
            AssertUnder(buttons, message, window);

            AppHarness.SaveScreen(window, "the-notice-on-linux.png");
        }, new AppOptions { Look = Look.Linux });
    }

    /// <summary>The notice on Windows, in dark: app name, title, message and three buttons side by side.</summary>
    [Fact(DisplayName = "the notice on Windows")]
    public Task TheNoticeOnWindows()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This scenario applies on Windows only.");
        return AppHarness.RunAsync(async app =>
        {
            var window = await ShowNoticeAsync(app);

            var texts = AppHarness.Texts(window).ToList();
            var appIndex = texts.IndexOf(AppName);
            var titleIndex = texts.IndexOf(Title);
            var messageIndex = texts.IndexOf(Message);
            Assert.True(appIndex >= 0, "\"Shapeshifter\" is shown next to the icon.");
            Assert.True(titleIndex > appIndex, "The title follows the app name.");
            Assert.True(messageIndex > titleIndex, "The message follows the title.");

            var message = FindText(window, Message);
            var buttons = ButtonsInOrder(window);
            AssertInARow(buttons, window);
            AssertUnder(buttons, message, window);

            AppHarness.SaveScreen(window, "the-notice-on-windows.png");
        }, new AppOptions { Look = Look.Windows, Dark = true });
    }

    /// <summary>The notice on a Mac: a banner with the title, the message and the three buttons under it.</summary>
    [Fact(DisplayName = "the notice on a Mac")]
    public Task TheNoticeOnAMac()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "This scenario applies on macOS only.");
        return AppHarness.RunAsync(async app =>
        {
            var window = await ShowNoticeAsync(app);

            var texts = AppHarness.Texts(window).ToList();
            var titleIndex = texts.IndexOf(Title);
            var messageIndex = texts.IndexOf(Message);
            Assert.True(titleIndex >= 0, "The title is shown.");
            Assert.True(messageIndex > titleIndex, "The message is shown below the title.");

            var message = FindText(window, Message);
            var buttons = ButtonsInOrder(window);
            AssertUnder(buttons, message, window);

            AppHarness.SaveScreen(window, "the-notice-on-a-mac.png");
        }, new AppOptions { Look = Look.MacOS });
    }

    /// <summary>The notice in German: every text is translated and the message wraps rather than being cut off.</summary>
    [Fact(DisplayName = "the notice in German")]
    public Task TheNoticeInGerman()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This scenario applies on Linux only.");
        return AppHarness.RunAsync(async app =>
        {
            var window = await ShowNoticeAsync(app);

            var texts = AppHarness.Texts(window).ToList();
            Assert.DoesNotContain(Title, texts);
            Assert.DoesNotContain(Message, texts);

            var buttons = AppHarness.Find<Button>(window).ToList();
            Assert.Equal(3, buttons.Count);
            var labels = buttons.Select(b => b.Content?.ToString() ?? string.Empty).ToList();
            Assert.All(labels, label => Assert.False(string.IsNullOrWhiteSpace(label)));
            Assert.DoesNotContain(Donate, labels);
            Assert.DoesNotContain(Already, labels);
            Assert.DoesNotContain(Later, labels);

            // The message is the longest text the card shows.
            var message = AppHarness.Find<TextBlock>(window)
                .Where(t => !string.IsNullOrEmpty(t.Text))
                .OrderByDescending(t => t.Text!.Length)
                .FirstOrDefault();
            Assert.NotNull(message);
            Assert.NotEqual(TextWrapping.NoWrap, message.TextWrapping);
            Assert.Equal(TextTrimming.None, message.TextTrimming);
            Assert.True(AppHarness.IsFullyShown(message), "The message is shown in full.");

            AppHarness.SaveScreen(window, "the-notice-in-german.png");
        }, new AppOptions { Look = Look.Linux, Language = "de" });
    }

    private static async Task<Window> ShowNoticeAsync(AppHarness app)
    {
        var shown = await app.ShowDonationNoticeAsync();
        Assert.True(shown, "The donation notice shows.");
        Assert.True(app.DonationNoticeVisible, "The donation notice is up.");
        var window = app.DonationNotice;
        Assert.NotNull(window);
        return window;
    }

    private static TextBlock FindText(Window window, string text)
    {
        var block = AppHarness.Find<TextBlock>(window).FirstOrDefault(t => t.Text == text);
        Assert.NotNull(block);
        return block;
    }

    private static List<Button> ButtonsInOrder(Window window)
    {
        var buttons = AppHarness.Find<Button>(window).ToList();
        var labels = buttons.Select(b => b.Content?.ToString()).ToList();
        Assert.Equal(new[] { Donate, Already, Later }, labels);
        return buttons;
    }

    private static Point TopLeft(Control control, Window window)
    {
        var point = control.TranslatePoint(default, window);
        Assert.NotNull(point);
        return point.Value;
    }

    private static void AssertInARow(List<Button> buttons, Window window)
    {
        for (var i = 1; i < buttons.Count; i++)
        {
            var previous = TopLeft(buttons[i - 1], window);
            var current = TopLeft(buttons[i], window);
            Assert.True(Math.Abs(previous.Y - current.Y) < 1, "The buttons sit in one row.");
            Assert.True(current.X > previous.X, "The buttons run left to right in order.");
        }
    }

    private static void AssertUnder(List<Button> buttons, TextBlock message, Window window)
    {
        var messageBottom = TopLeft(message, window).Y + message.Bounds.Height;
        for (var i = 0; i < buttons.Count; i++)
        {
            Assert.True(TopLeft(buttons[i], window).Y >= messageBottom - 0.5, "The buttons are under the message.");
        }
    }
}
