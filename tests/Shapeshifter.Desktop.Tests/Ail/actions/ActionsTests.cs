// ail-derived-from: Actions@adb0f4c4431849b6
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Actions flow: opening, moving through, leaving and running the actions in the picker.</summary>
public sealed class ActionsTests
{
    /// <summary>Right opens the actions, Down chooses the next one, and letting go of Ctrl runs it: the text becomes a file on the clipboard.</summary>
    [Fact(DisplayName = "Right opens the actions, and letting go of Ctrl runs the chosen one")]
    public Task RightOpensTheActionsAndLettingGoOfCtrlRunsTheChosenOne() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("memo", null);

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.ReleaseCtrlAsync();

        Assert.False(app.PickerVisible);
        foreach (var pasted in app.Paste.Pasted)
        {
            Assert.True(string.IsNullOrEmpty(pasted.Text), "nothing is typed into the text box");
        }

        var files = app.Clipboard.Current.Files;
        Assert.Equal(1, files.Count);
        var path = files[0];
        Assert.Matches(new Regex(@"^Text .+ .+\.txt$"), Path.GetFileName(path));
        Assert.Equal("memo", await File.ReadAllTextAsync(path));

        AppHarness.SaveScreen(app.Picker, "right-opens-the-actions-and-letting-go-of-ctrl-runs-the-chos.png");
    });

    /// <summary>Left leaves the actions, so Down moves through the history again and the item it reaches is pasted.</summary>
    [Fact(DisplayName = "Left goes back to the history")]
    public Task LeftGoesBackToTheHistory() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("ant", null);
        await app.CopyAsync("bee", null);
        await app.CopyAsync("cat", null);

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.TapAsync(GestureKey.Left);
        await app.TapAsync(GestureKey.Down);
        await app.ReleaseCtrlAsync();

        Assert.Equal(1, app.Paste.Pasted.Count);
        Assert.Equal("bee", app.Paste.Pasted[0].Text);

        AppHarness.SaveScreen(app.Picker, "left-goes-back-to-the-history.png");
    });

    /// <summary>Moving to another item with V drops the chosen action, so the new item is pasted as it was copied.</summary>
    [Fact(DisplayName = "choosing another item starts again at Paste")]
    public Task ChoosingAnotherItemStartsAgainAtPaste() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("owl", null);
        await app.CopyAsync("fox", null);

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.TapAsync(GestureKey.Trigger);
        await app.ReleaseCtrlAsync();

        Assert.Equal(1, app.Paste.Pasted.Count);
        Assert.Equal("owl", app.Paste.Pasted[0].Text);

        AppHarness.SaveScreen(app.Picker, "choosing-another-item-starts-again-at-paste.png");
    });

    /// <summary>Escape while the actions are open cancels the gesture: nothing is pasted and the clipboard stays as it was.</summary>
    [Fact(DisplayName = "Escape in the actions cancels without running anything")]
    public Task EscapeInTheActionsCancelsWithoutRunningAnything() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("calm", null);
        var writesBefore = app.Clipboard.Written.Count;

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.TapAsync(GestureKey.Escape);
        await app.ReleaseCtrlAsync();

        Assert.Equal(0, app.Paste.Pasted.Count);
        Assert.Equal(writesBefore, app.Clipboard.Written.Count);
        Assert.Equal("calm", app.Clipboard.Current.Text);

        AppHarness.SaveScreen(app.Picker, "escape-in-the-actions-cancels-without-running-anything.png");
    });
}
