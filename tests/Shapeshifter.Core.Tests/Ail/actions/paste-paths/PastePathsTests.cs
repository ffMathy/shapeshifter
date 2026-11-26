// ail-derived-from: PastePaths@db5be7c25da4199d
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the PastePaths node: the paste-paths action and its Join.</summary>
public sealed class PastePathsTests
{
    /// <summary>A single path is returned as it was copied.</summary>
    [Fact(DisplayName = "example 1: Join([\"/home/anna/report.pdf\"]) => \"/home/anna/report.pdf\"")]
    public void Example1JoinSinglePath()
    {
        var joined = PastePathsAction.Join(["/home/anna/report.pdf"]);

        Assert.Equal("/home/anna/report.pdf", joined);
    }

    /// <summary>On Linux, paths are joined by LF with none after the last.</summary>
    [Fact(DisplayName = "example 2: Join([\"/a/x.txt\", \"/a/y.txt\"]) on Linux => \"/a/x.txt\\n/a/y.txt\"")]
    public void Example2JoinOnLinuxUsesLf()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This example applies on Linux only.");

        var joined = PastePathsAction.Join(["/a/x.txt", "/a/y.txt"]);

        Assert.Equal("/a/x.txt\n/a/y.txt", joined);
    }

    /// <summary>On Windows, paths are joined by CR LF with none after the last.</summary>
    [Fact(DisplayName = "example 3: Join([\"C:\\\\a\\\\x.txt\", \"C:\\\\a\\\\y.txt\"]) on Windows => \"C:\\\\a\\\\x.txt\\r\\nC:\\\\a\\\\y.txt\"")]
    public void Example3JoinOnWindowsUsesCrLf()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "This example applies on Windows only.");

        var joined = PastePathsAction.Join(["C:\\a\\x.txt", "C:\\a\\y.txt"]);

        Assert.Equal("C:\\a\\x.txt\r\nC:\\a\\y.txt", joined);
    }

    /// <summary>A path with a space is not quoted.</summary>
    [Fact(DisplayName = "example 4: Join([\"/a/My file.txt\"]) => \"/a/My file.txt\"; no quotes are added")]
    public void Example4JoinAddsNoQuotes()
    {
        var joined = PastePathsAction.Join(["/a/My file.txt"]);

        Assert.Equal("/a/My file.txt", joined);
    }

    /// <summary>The title says path for one file and paths for several.</summary>
    [Fact(DisplayName = "example 5: GetTitle(files [\"/a/x.txt\"]) => \"Paste path\";  GetTitle(files [\"/a/x.txt\", \"/a/y.txt\"]) => \"Paste paths\"")]
    public void Example5TitleSaysPathOrPaths()
    {
        using var harness = new ActionTestHarness();
        var one = harness.AddToHistory(new ClipboardSnapshot { Files = ["/a/x.txt"] });
        var two = harness.AddToHistory(new ClipboardSnapshot { Files = ["/a/x.txt", "/a/y.txt"] });
        var action = new PastePathsAction();

        Assert.Equal("Paste path", action.GetTitle(one));
        Assert.Equal("Paste paths", action.GetTitle(two));
    }

    /// <summary>The action applies to a copied file.</summary>
    [Fact(DisplayName = "example 6: CanExecute(files [\"/a/x.txt\"]) => true")]
    public void Example6CanExecuteOnFile()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = ["/a/x.txt"] });

        Assert.True(new PastePathsAction().CanExecute(item));
    }

    /// <summary>The action does not apply to text, even text that looks like a path.</summary>
    [Fact(DisplayName = "example 7: CanExecute(text \"/a/x.txt\") => false")]
    public void Example7CannotExecuteOnText()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "/a/x.txt" });

        Assert.False(new PastePathsAction().CanExecute(item));
    }

    /// <summary>The action does not apply to an image.</summary>
    [Fact(DisplayName = "example 8: CanExecute(PNG image) => false")]
    public void Example8CannotExecuteOnImage()
    {
        using var harness = new ActionTestHarness();
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = png });

        Assert.False(new PastePathsAction().CanExecute(item));
    }

    /// <summary>Executing pastes the joined paths as text and leaves the history unchanged.</summary>
    [Fact(DisplayName = "example 9: ExecuteAsync(files [\"/a/x.txt\", \"/a/y.txt\"]) => pastes { Text: Join([\"/a/x.txt\", \"/a/y.txt\"]) }; the history is unchanged")]
    public async Task Example9ExecutePastesJoinedPaths()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = ["/a/x.txt", "/a/y.txt"] });
        var historyBefore = harness.History.Items.Select(i => i.Id).ToList();

        await new PastePathsAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal(PastePathsAction.Join(["/a/x.txt", "/a/y.txt"]), written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Select(i => i.Id).ToList());
    }
}
