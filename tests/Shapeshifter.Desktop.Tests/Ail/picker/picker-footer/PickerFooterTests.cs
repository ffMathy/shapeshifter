// ail-derived-from: PickerFooter@c4152e526476394b
using System.Reflection;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for PickerFooter: the key hints at the foot of the picker.</summary>
public sealed class PickerFooterTests
{
    /// <summary>While Ctrl is held, the footer shows the hints for choosing and releasing.</summary>
    [Fact(DisplayName = "while Ctrl is held")]
    public Task WhileCtrlIsHeld() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Monday", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        AssertShowsInOrder(
            AppHarness.Texts(app.Picker!),
            "Ctrl", "Release to paste",
            "Space", "Keep open",
            "V", "↑↓", "Move",
            "1–9", "Jump",
            "←→", "Actions",
            "S", "Search",
            "Del", "Remove",
            "Esc", "Cancel");

        AppHarness.SaveScreen(app.Picker!, "while-ctrl-is-held.png");
    }, new AppOptions { Language = "en" });

    /// <summary>Once kept open with Space, the footer shows Enter to paste and Esc to close.</summary>
    [Fact(DisplayName = "while it is kept open")]
    public Task WhileItIsKeptOpen() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Monday", null);
        await app.HoldCtrlVAsync();

        await TapAsync(app, "Space");

        Assert.True(app.PickerVisible);
        AssertShowsInOrder(
            AppHarness.Texts(app.Picker!),
            "Enter", "Paste",
            "V", "↑↓", "Move",
            "1–9", "Jump",
            "←→", "Actions",
            "S", "Search",
            "Del", "Remove",
            "Esc", "Close");

        AppHarness.SaveScreen(app.Picker!, "while-it-is-kept-open.png");
    }, new AppOptions { Language = "en" });

    /// <summary>While searching, the footer shows only the hints that still apply.</summary>
    [Fact(DisplayName = "while searching")]
    public Task WhileSearching() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Monday", null);
        await app.HoldCtrlVAsync();

        await TapAsync(app, "Search");

        Assert.True(app.PickerVisible);
        AssertShowsInOrder(
            AppHarness.Texts(app.Picker!),
            "Enter", "Paste",
            "↑↓", "Move",
            "←→", "Actions",
            "Esc", "Cancel");

        AppHarness.SaveScreen(app.Picker!, "while-searching.png");
    }, new AppOptions { Language = "en" });

    /// <summary>In the Mac look on Cmd, the modifier cap is ⌘ and the header names the key to release or Enter.</summary>
    [Fact(DisplayName = "the Mac look")]
    public Task TheMacLook() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("Meeting notes for Monday", null);
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var held = ToArray(AppHarness.Texts(app.Picker!));
        AssertShowsInOrder(held, "⌘", "Release to paste");
        Assert.Contains("Release ⌘ to paste", held);

        await TapAsync(app, "Space");

        Assert.True(app.PickerVisible);
        var kept = ToArray(AppHarness.Texts(app.Picker!));
        Assert.Contains("Press Enter to paste", kept);

        AppHarness.SaveScreen(app.Picker!, "the-mac-look.png");
    }, MacOptions());

    private static AppOptions MacOptions()
    {
        object options = new AppOptions { Modifier = "Cmd", Language = "en" };
        var lookProperty = typeof(AppOptions).GetProperty("Look")
            ?? throw new InvalidOperationException("AppOptions has no Look.");
        lookProperty.SetValue(options, NamedValue(lookProperty.PropertyType, "MacOS"));
        return (AppOptions)options;
    }

    private static async Task TapAsync(object app, string key)
    {
        var tap = Array.Find(
            app.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance),
            m => m.Name == "TapAsync" && m.GetParameters().Length == 1)
            ?? throw new InvalidOperationException("The harness has no TapAsync(key).");
        var result = tap.Invoke(app, [NamedValue(tap.GetParameters()[0].ParameterType, key)]);
        switch (result)
        {
            case Task task:
                await task;
                break;
            case ValueTask valueTask:
                await valueTask;
                break;
            default:
                break;
        }
    }

    private static object NamedValue(Type type, string name)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, name);
        }

        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        if (property is not null)
        {
            return property.GetValue(null)!;
        }

        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(type.Name + " has no " + name + ".");
        return field.GetValue(null)!;
    }

    private static string[] ToArray(IEnumerable<string> texts) => [.. texts];

    private static void AssertShowsInOrder(IEnumerable<string> shown, params string[] expected)
    {
        var texts = ToArray(shown);
        for (var start = 0; start + expected.Length <= texts.Length; start++)
        {
            var matches = true;
            for (var i = 0; i < expected.Length; i++)
            {
                if (!string.Equals(texts[start + i], expected[i], StringComparison.Ordinal))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return;
            }
        }

        Assert.Fail(
            "Expected the footer to show, in order: [" + string.Join(" | ", expected) +
            "] but the picker shows: [" + string.Join(" | ", texts) + "]");
    }
}
