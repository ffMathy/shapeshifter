// ail-derived-from: Actions@adb0f4c4431849b6
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Actions flow: opening, moving through, leaving and running the actions in the picker.</summary>
public sealed partial class ActionsTests
{
    /// <summary>Matches the name of a file made from text: "Text &lt;date&gt; &lt;time&gt;.txt".</summary>
    [GeneratedRegex(@"^Text \S.* \S.*\.txt$")]
    private static partial Regex TextFileName();

    /// <summary>Collects every existing file path the given clipboard contents carry.</summary>
    private static List<string> FilePathsIn(object? contents)
    {
        var paths = new List<string>();
        if (contents is null)
        {
            return paths;
        }

        foreach (var property in contents.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            object? value;
            try
            {
                value = property.GetValue(contents);
            }
            catch (TargetInvocationException)
            {
                continue;
            }

            if (value is string single)
            {
                AddIfFile(paths, single);
            }
            else if (value is IEnumerable many)
            {
                foreach (var entry in many)
                {
                    if (entry is string path)
                    {
                        AddIfFile(paths, path);
                    }
                }
            }
        }

        return paths;
    }

    private static void AddIfFile(List<string> paths, string candidate)
    {
        if (Path.IsPathRooted(candidate) && File.Exists(candidate) && !paths.Contains(candidate))
        {
            paths.Add(candidate);
        }
    }

    /// <summary>Scenario: Right opens the actions, and letting go of Ctrl runs the chosen one.</summary>
    [Fact(DisplayName = "Right opens the actions, and letting go of Ctrl runs the chosen one")]
    public Task RightOpensTheActionsAndLettingGoOfCtrlRunsTheChosenOne() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("memo", null);

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.ReleaseCtrlAsync();

        Assert.False(app.PickerVisible);
        Assert.Empty(app.Paste.Pasted);

        var files = FilePathsIn(app.Clipboard.Current);
        Assert.Single(files);
        var file = files[0];
        Assert.Matches(TextFileName(), Path.GetFileName(file));
        Assert.Equal("memo", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));

        AppHarness.SaveScreen(app.Picker, "right-opens-the-actions-and-letting-go-of-ctrl-runs-the-chos.png");
    });

    /// <summary>Scenario: Left goes back to the history.</summary>
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

        Assert.Equal(["bee"], app.Paste.Pasted);

        AppHarness.SaveScreen(app.Picker, "left-goes-back-to-the-history.png");
    });

    /// <summary>Scenario: choosing another item starts again at Paste.</summary>
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

        Assert.Equal(["owl"], app.Paste.Pasted);

        AppHarness.SaveScreen(app.Picker, "choosing-another-item-starts-again-at-paste.png");
    });

    /// <summary>Scenario: Escape in the actions cancels without running anything.</summary>
    [Fact(DisplayName = "Escape in the actions cancels without running anything")]
    public Task EscapeInTheActionsCancelsWithoutRunningAnything() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("calm", null);

        var clipboardBefore = app.Clipboard.Current;
        var writesBefore = app.Clipboard.Written.Count;

        await app.HoldCtrlVAsync();
        await app.TapAsync(GestureKey.Right);
        await app.TapAsync(GestureKey.Down);
        await app.TapAsync(GestureKey.Escape);
        await app.ReleaseCtrlAsync();

        Assert.Empty(app.Paste.Pasted);
        Assert.Equal(writesBefore, app.Clipboard.Written.Count);
        Assert.Equal(clipboardBefore, app.Clipboard.Current);

        AppHarness.SaveScreen(app.Picker, "escape-in-the-actions-cancels-without-running-anything.png");
    });
}
