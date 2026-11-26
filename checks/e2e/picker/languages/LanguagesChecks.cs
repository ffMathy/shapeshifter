// ail-derived-from: Languages@988032379fe28687
// End-to-end check for Languages: the picker in the language SHAPESHIFTER_LANGUAGE names, laid out from right to left
// in Arabic. Each scenario starts Shapeshifter on a desktop of its own with that variable set, as a user would, and
// reads the picker back from the screen (OCR, English letters only).
using static Shapeshifter.Checks.PickerDriver;

namespace Shapeshifter.Checks;

public sealed class LanguagesChecks : Scenario
{
    static async Task WithLanguage(string language, Func<string, Task> body)
    {
        var desktop = await OwnDesktop.Start(1280, 800, $"language-{language}", new Dictionary<string, string> { ["SHAPESHIFTER_LANGUAGE"] = language });
        try
        {
            await body(desktop.Display);
        }
        finally
        {
            await ReleaseAll(desktop.Display);
            await desktop.Stop();
        }
    }

    [Fact(DisplayName = "the picker speaks the language SHAPESHIFTER_LANGUAGE names", Timeout = 180_000)]
    public Task SpeaksTheLanguage() => WithLanguage("de", async d =>
    {
        var box = await HoldThePickerOpen(d);
        var text = Squash(ReadTextBothWays(ShootPicker(box, d)));
        await CancelThePicker(d);
        Assert.Contains("Noch nichts kopiert", text, StringComparison.Ordinal);
        Assert.Contains("Strg", text, StringComparison.Ordinal);
        Assert.Contains("Abbrechen", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Nothing copied yet", text, StringComparison.Ordinal);
    });

    [Fact(DisplayName = "the Linux look's list headings stay in English", Timeout = 180_000)]
    public Task HeadingsStayInEnglish() => WithLanguage("de", async d =>
    {
        await CopyWithXclip("ein kurzer Text", null, d);
        var box = await HoldThePickerOpen(d);
        var text = Squash(ReadTextBothWays(ShootPicker(box, d)));
        await CancelThePicker(d);
        Assert.Contains("Gerade eben", text, StringComparison.Ordinal);
        Assert.Contains("History", text, StringComparison.Ordinal);
        Assert.Contains("Actions", text, StringComparison.Ordinal);
        Assert.Contains("to choose", text, StringComparison.Ordinal);
    });

    [Fact(DisplayName = "in Arabic the picker is laid out from right to left", Timeout = 300_000)]
    public async Task ArabicIsRightToLeft()
    {
        foreach (var (language, side) in new[] { ("de", "left"), ("ar", "right") })
        {
            await WithLanguage(language, async d =>
            {
                var box = await HoldThePickerOpen(d);
                var png = ShootPicker(box, d);
                await CancelThePicker(d);
                // The first key in the footer, Ctrl (Strg in German), starts the reading order: on the left, or on the right.
                // The footer is the lowest place the key's name is written (the empty picker's hint names it too).
                var key = language == "de" ? "Strg" : "Ctrl";
                var first = ReadWords(png).Where(w => w.Text.Contains(key, StringComparison.Ordinal)).OrderByDescending(w => w.Top).Cast<Word?>().FirstOrDefault();
                Assert.NotNull(first);
                var middle = box.Width; // the screenshot is twice the picker's size
                Assert.Equal(side, first.Value.Left < middle ? "left" : "right");
            });
        }
    }
}
