// ail-derived-from: PickerFooter@c4152e526476394b
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the PickerFooter scenarios: the key hints the picker shows in each of its states.</summary>
public sealed class PickerFooterTests
{
    private static readonly string[] HeldHints =
    [
        "Ctrl", "Release to paste",
        "Space", "Keep open",
        "V", "↑↓", "Move",
        "1–9", "Jump",
        "←→", "Actions",
        "S", "Search",
        "Del", "Remove",
        "Esc", "Cancel",
    ];

    private static readonly string[] KeptOpenHints =
    [
        "Enter", "Paste",
        "V", "↑↓", "Move",
        "1–9", "Jump",
        "←→", "Actions",
        "S", "Search",
        "Del", "Remove",
        "Esc", "Close",
    ];

    private static readonly string[] SearchingHints =
    [
        "Enter", "Paste",
        "↑↓", "Move",
        "←→", "Actions",
        "Esc", "Cancel",
    ];

    /// <summary>Scenario: while Ctrl is held.</summary>
    [Fact(DisplayName = "while Ctrl is held")]
    public Task WhileCtrlIsHeld() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Friday", "Notes");

        await app.HoldCtrlVAsync();

        await AppHarness.WaitUntilAsync(
            () => app.PickerVisible && IndexOfRun(AppHarness.Texts(app.Picker!), HeldHints) >= 0,
            "the footer shows the hints for while Ctrl is held");
        var texts = AppHarness.Texts(app.Picker!);
        Assert.True(IndexOfRun(texts, HeldHints) >= 0, Describe(HeldHints, texts));

        AppHarness.SaveScreen(app.Picker!, "while-ctrl-is-held.png");
    });

    /// <summary>Scenario: while it is kept open.</summary>
    [Fact(DisplayName = "while it is kept open")]
    public Task WhileItIsKeptOpen() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Friday", "Notes");
        await app.HoldCtrlVAsync();

        await app.TapAsync(GestureKey.Space);

        await AppHarness.WaitUntilAsync(
            () => app.PickerVisible && IndexOfRun(AppHarness.Texts(app.Picker!), KeptOpenHints) >= 0,
            "the footer shows the hints for while it is kept open");
        var texts = AppHarness.Texts(app.Picker!);
        Assert.True(IndexOfRun(texts, KeptOpenHints) >= 0, Describe(KeptOpenHints, texts));

        AppHarness.SaveScreen(app.Picker!, "while-it-is-kept-open.png");
    });

    /// <summary>Scenario: while searching.</summary>
    [Fact(DisplayName = "while searching")]
    public Task WhileSearching() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Friday", "Notes");
        await app.HoldCtrlVAsync();

        await app.TapAsync(GestureKey.Search);

        await AppHarness.WaitUntilAsync(
            () => app.PickerVisible && IndexOfRun(AppHarness.Texts(app.Picker!), SearchingHints) >= 0,
            "the footer shows the hints for while searching");
        var texts = AppHarness.Texts(app.Picker!);
        Assert.True(IndexOfRun(texts, SearchingHints) >= 0, Describe(SearchingHints, texts));

        AppHarness.SaveScreen(app.Picker!, "while-searching.png");
    });

    /// <summary>Scenario: the Mac look.</summary>
    [Fact(DisplayName = "the Mac look")]
    public Task TheMacLook() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Friday", "Notes");
        await app.HoldCtrlVAsync();

        await AppHarness.WaitUntilAsync(
            () => app.PickerVisible && AppHarness.Texts(app.Picker!).Contains("Release ⌘ to paste"),
            "the header says Release ⌘ to paste");
        var held = AppHarness.Texts(app.Picker!);
        Assert.Contains("Release ⌘ to paste", held);
        Assert.DoesNotContain("Ctrl", held);
        var cmd = IndexOf(held, "⌘");
        var space = IndexOf(held, "Space");
        Assert.True(cmd >= 0, "the key cap ⌘ is shown: " + string.Join(" | ", held));
        Assert.True(space > cmd, "⌘ is the first key cap, before Space: " + string.Join(" | ", held));

        await app.TapAsync(GestureKey.Space);

        await AppHarness.WaitUntilAsync(
            () => app.PickerVisible && AppHarness.Texts(app.Picker!).Contains("Press Enter to paste"),
            "the header says Press Enter to paste once kept open");
        var kept = AppHarness.Texts(app.Picker!);
        Assert.Contains("Press Enter to paste", kept);

        AppHarness.SaveScreen(app.Picker!, "the-mac-look.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd" });

    private static int IndexOf(IReadOnlyList<string> texts, string text)
    {
        for (var i = 0; i < texts.Count; i++)
        {
            if (texts[i] == text)
            {
                return i;
            }
        }

        return -1;
    }

    private static int IndexOfRun(IReadOnlyList<string> texts, string[] run)
    {
        for (var start = 0; start + run.Length <= texts.Count; start++)
        {
            var matches = true;
            for (var j = 0; j < run.Length; j++)
            {
                if (texts[start + j] != run[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return start;
            }
        }

        return -1;
    }

    private static string Describe(string[] expected, IReadOnlyList<string> texts) =>
        "expected the footer to show, in order: " + string.Join(" | ", expected)
        + "\nbut the picker shows: " + string.Join(" | ", texts);
}
