// ail-derived-from: Refraction@51acbb29a86868df
using System;
using System.Threading.Tasks;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Refraction look: the picker on the Mac.</summary>
public sealed class RefractionTests
{
    private static string HeaderText(AppHarness app)
    {
        Assert.NotNull(app.Picker);
        return string.Join(" ", AppHarness.Texts(app.Picker!));
    }

    /// <summary>Scenario: the Mac picker.</summary>
    [Fact(DisplayName = "the Mac picker")]
    public Task TheMacPicker() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied line", "TextEdit");
        await app.CopyAsync("second copied line", "TextEdit");
        await app.CopyAsync("third copied line", "Safari");
        await app.CopyAsync("fourth copied line", "Notes");
        await app.CopyAsync("fifth copied line", "Mail");

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var shown = HeaderText(app);

        var title = shown.IndexOf("Clipboard", StringComparison.Ordinal);
        var count = shown.IndexOf("5 items", StringComparison.Ordinal);
        var hint = shown.IndexOf("Release ⌘ to paste", StringComparison.Ordinal);
        Assert.True(title >= 0, "the header says \"Clipboard\"");
        Assert.True(count > title, "\"5 items\" follows the title");
        Assert.True(hint > count, "\"Release ⌘ to paste\" is at the header's end");

        Assert.NotNull(app.ChosenRow);
        Assert.True(app.ChosenRow!.IsSelected);

        AppHarness.SaveScreen(app.Picker!, "the-mac-picker.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd" });

    /// <summary>Scenario: kept open.</summary>
    [Fact(DisplayName = "kept open")]
    public Task KeptOpen() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied line", "TextEdit");
        await app.CopyAsync("second copied line", "Safari");

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Space);

        Assert.True(app.PickerVisible);
        var shown = HeaderText(app);
        Assert.Contains("Press Enter to paste", shown, StringComparison.Ordinal);
        Assert.DoesNotContain("Release ⌘ to paste", shown, StringComparison.Ordinal);

        AppHarness.SaveScreen(app.Picker!, "kept-open.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd" });

    /// <summary>Scenario: without the system's material.</summary>
    [Fact(DisplayName = "without the system's material")]
    public Task WithoutTheSystemsMaterial() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied line", "TextEdit");

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.NotNull(app.Picker);

        AppHarness.SaveScreen(app.Picker!, "without-the-system-s-material.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd", NoCompositor = true });
}
