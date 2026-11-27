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

    /// <summary>Taps the gesture key with the given name, as app.TapAsync(GestureKey.Name) does.</summary>
    private static Task TapAsync(object app, string key)
    {
        var method = app.GetType()
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(m => m.Name == "TapAsync" && m.GetParameters().Length == 1);
        var keyValue = Enum.Parse(method.GetParameters()[0].ParameterType, key);
        object? result;
        try
        {
            result = method.Invoke(app, [keyValue]);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            throw e.InnerException;
        }

        return result switch
        {
            Task task => task,
            ValueTask valueTask => valueTask.AsTask(),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>Reads the plain text a pasted snapshot carries.</summary>
    private static string? TextOf(object? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        if (snapshot is string s)
        {
            return s;
        }

        var properties = snapshot.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.PropertyType == typeof(string))
            .ToList();
        foreach (var name in new[] { "Text", "PlainText" })
        {
            var named = properties.FirstOrDefault(p => p.Name == name);
            if (named is not null)
            {
                return (string?)named.GetValue(snapshot);
            }
        }

        foreach (var property in properties)
        {
            if (property.GetValue(snapshot) is string value)
            {
                return value;
            }
        }

        return null;
    }

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
        await TapAsync(app, "Right");
        await TapAsync(app, "Down");
        await app.ReleaseCtrlAsync();

        Assert.False(app.PickerVisible);
        Assert.Empty(app.Paste.Pasted);

        var files = FilePathsIn(app.Clipboard.Current);
        Assert.Single(files);
        var file = files[0];
        Assert.Matches(TextFileName(), Path.GetFileName(file));
        Assert.Equal("memo", await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken));

        var picker = app.Picker;
        Assert.NotNull(picker);
        AppHarness.SaveScreen(picker, "right-opens-the-actions-and-letting-go-of-ctrl-runs-the-chos.png");
    });

    /// <summary>Scenario: Left goes back to the history.</summary>
    [Fact(DisplayName = "Left goes back to the history")]
    public Task LeftGoesBackToTheHistory() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("ant", null);
        await app.CopyAsync("bee", null);
        await app.CopyAsync("cat", null);

        await app.HoldCtrlVAsync();
        await TapAsync(app, "Right");
        await TapAsync(app, "Down");
        await TapAsync(app, "Left");
        await TapAsync(app, "Down");
        await app.ReleaseCtrlAsync();

        var pasted = app.Paste.Pasted;
        Assert.Single(pasted);
        Assert.Equal("bee", TextOf(pasted[0]));

        var picker = app.Picker;
        Assert.NotNull(picker);
        AppHarness.SaveScreen(picker, "left-goes-back-to-the-history.png");
    });

    /// <summary>Scenario: choosing another item starts again at Paste.</summary>
    [Fact(DisplayName = "choosing another item starts again at Paste")]
    public Task ChoosingAnotherItemStartsAgainAtPaste() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("owl", null);
        await app.CopyAsync("fox", null);

        await app.HoldCtrlVAsync();
        await TapAsync(app, "Right");
        await TapAsync(app, "Down");
        await TapAsync(app, "Trigger");
        await app.ReleaseCtrlAsync();

        var pasted = app.Paste.Pasted;
        Assert.Single(pasted);
        Assert.Equal("owl", TextOf(pasted[0]));

        var picker = app.Picker;
        Assert.NotNull(picker);
        AppHarness.SaveScreen(picker, "choosing-another-item-starts-again-at-paste.png");
    });

    /// <summary>Scenario: Escape in the actions cancels without running anything.</summary>
    [Fact(DisplayName = "Escape in the actions cancels without running anything")]
    public Task EscapeInTheActionsCancelsWithoutRunningAnything() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("calm", null);

        var clipboardBefore = app.Clipboard.Current;
        var writesBefore = app.Clipboard.Written.Count;

        await app.HoldCtrlVAsync();
        await TapAsync(app, "Right");
        await TapAsync(app, "Down");
        await TapAsync(app, "Escape");
        await app.ReleaseCtrlAsync();

        Assert.Empty(app.Paste.Pasted);
        Assert.Equal(writesBefore, app.Clipboard.Written.Count);
        Assert.Equal(clipboardBefore, app.Clipboard.Current);

        var picker = app.Picker;
        Assert.NotNull(picker);
        AppHarness.SaveScreen(picker, "escape-in-the-actions-cancels-without-running-anything.png");
    });
}
