// ail-derived-from: MicaSheets@c3529f4f680c45ee
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Mica Sheets look of the picker on Windows 11.</summary>
public sealed class MicaSheetsTests
{
    /// <summary>Scenario: light.</summary>
    [Fact(DisplayName = "light")]
    public Task Light() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied text", null);
        await app.CopyAsync("second copied text", null);
        await app.CopyAsync("third copied text", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var texts = AppHarness.Texts(app.Picker!).ToList();

        var title = texts.IndexOf("Clipboard history");
        Assert.True(title >= 0, "the header says \"Clipboard history\"");
        var count = texts.IndexOf("3 items");
        Assert.True(count > title, "the header shows \"3 items\" at its end, after the title");

        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);

        var heading = texts.IndexOf("Actions for this text");
        Assert.True(heading >= 0, "the actions panel says \"Actions for this text\"");
        var paste = texts.IndexOf("Paste", heading + 1);
        Assert.True(paste > heading, "the actions list \"Paste\"");
        var defaultMark = texts.IndexOf("Default", paste + 1);
        Assert.True(defaultMark > paste, "\"Default\" is at the end of \"Paste\"");

        AppHarness.SaveScreen(app.Picker!, "light.png");
    }, new AppOptions { Look = Look.Windows });

    /// <summary>Scenario: dark.</summary>
    [Fact(DisplayName = "dark")]
    public Task Dark() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("copied text", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var chosen = app.ChosenRow;
        Assert.NotNull(chosen);
        Assert.True(chosen.IsSelected);

        AppHarness.SaveScreen(app.Picker!, "dark.png");
    }, new AppOptions { Look = Look.Windows, Dark = true });
}
