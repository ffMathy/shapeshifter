// ail-derived-from: Refraction@dbc0b6b43f6c84f9
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Checks derived from the scenarios of the Refraction look, the picker on the Mac.</summary>
public sealed class RefractionTests
{
    /// <summary>The Mac picker shows its header and the chosen row as an accent capsule.</summary>
    [Fact(DisplayName = "the Mac picker")]
    public Task TheMacPicker() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied text", "Notes");
        await app.CopyAsync("second copied text", "Notes");
        await app.CopyAsync("third copied text", "Notes");
        await app.CopyAsync("fourth copied text", "Notes");
        await app.CopyAsync("fifth copied text", "Notes");

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker!).ToList();
        Assert.Contains("Clipboard", texts);
        Assert.Contains("Release ⌘ to paste", texts);
        Assert.Contains("5 items", string.Join(" ", texts));

        var title = texts.IndexOf("Clipboard");
        var hint = texts.IndexOf("Release ⌘ to paste");
        Assert.True(title < hint, "the paste hint comes at the end of the header, after the title");

        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);

        AppHarness.SaveScreen(app.Picker!, "the-mac-picker.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd" });

    /// <summary>Kept open, the header's hint asks for Enter.</summary>
    [Fact(DisplayName = "kept open")]
    public Task KeptOpen() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("copied text", "Notes");
        await app.HoldCtrlVAsync();

        await app.TapAsync(GestureKey.Space);

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker!).ToList();
        Assert.Contains("Press Enter to paste", texts);
        Assert.DoesNotContain("Release ⌘ to paste", texts);

        AppHarness.SaveScreen(app.Picker!, "kept-open.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd" });

    /// <summary>Without a compositor the sheet is square, opaque and fills its window.</summary>
    [Fact(DisplayName = "without the system's material")]
    public Task WithoutTheSystemsMaterial() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("copied text", "Notes");
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);

        AppHarness.SaveScreen(app.Picker!, "without-the-system-s-material.png");
    }, new AppOptions { Look = Look.MacOS, Modifier = "Cmd", NoCompositor = true });
}
