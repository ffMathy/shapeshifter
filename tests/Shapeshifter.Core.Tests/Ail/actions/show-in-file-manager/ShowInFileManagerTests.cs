// ail-derived-from: ShowInFileManager@1462a6a69280de42
using System.Globalization;
using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the ShowInFileManager node's examples.</summary>
public sealed class ShowInFileManagerTests
{
    /// <summary>Checks that an existing file can be shown in the file manager.</summary>
    [Fact(DisplayName = "example 1: CanExecute(files [an existing file]) => true")]
    public void Example1CanExecuteExistingFile()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var file = Path.Combine(directory.FullName, "a.txt");
            File.WriteAllText(file, "a");
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Files = [file] });

            Assert.True(new ShowInFileManagerAction().CanExecute(item));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Checks that an existing folder can be shown in the file manager.</summary>
    [Fact(DisplayName = "example 2: CanExecute(files [an existing folder]) => true")]
    public void Example2CanExecuteExistingFolder()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var folder = Path.Combine(directory.FullName, "folder");
            Directory.CreateDirectory(folder);
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Files = [folder] });

            Assert.True(new ShowInFileManagerAction().CanExecute(item));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Checks that a path naming nothing cannot be shown.</summary>
    [Fact(DisplayName = "example 3: CanExecute(files [the path of nothing]) => false; it was moved or deleted since it was copied")]
    public void Example3CanExecuteMissingPath()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var missing = Path.Combine(directory.FullName, "missing.txt");
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Files = [missing] });

            Assert.False(new ShowInFileManagerAction().CanExecute(item));
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Checks that a network path is never shown.</summary>
    [Fact(DisplayName = "example 4: CanExecute(files [\"\\\\\\\\server\\\\share\\\\a.txt\"]) => false")]
    public void Example4CanExecuteNetworkPath()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Files = [@"\\server\share\a.txt"] });

        Assert.False(new ShowInFileManagerAction().CanExecute(item));
    }

    /// <summary>Checks that text cannot be shown in the file manager.</summary>
    [Fact(DisplayName = "example 5: CanExecute(text \"hello\") => false")]
    public void Example5CanExecuteText()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "hello" });

        Assert.False(new ShowInFileManagerAction().CanExecute(item));
    }

    /// <summary>Checks the description for several files.</summary>
    [Fact(DisplayName = "example 6: GetDescription(files [an existing file, another]) => \"Opens the folder these files are in, with them selected.\"")]
    public void Example6GetDescriptionSeveralFiles()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            var first = Path.Combine(directory.FullName, "a.txt");
            var second = Path.Combine(directory.FullName, "b.txt");
            File.WriteAllText(first, "a");
            File.WriteAllText(second, "b");
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });

            Assert.Equal(
                "Opens the folder these files are in, with them selected.",
                new ShowInFileManagerAction().GetDescription(item));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
            directory.Delete(recursive: true);
        }
    }

    /// <summary>Checks that executing reveals the first file and pastes nothing.</summary>
    [Fact(DisplayName = "example 7: ExecuteAsync(files [\"/home/anna/a.txt\", \"/home/anna/b.txt\"], both existing) => the launcher reveals \"/home/anna/a.txt\"; nothing is pasted")]
    public async Task Example7ExecuteRevealsFirstFile()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var first = Path.Combine(directory.FullName, "a.txt");
            var second = Path.Combine(directory.FullName, "b.txt");
            File.WriteAllText(first, "a");
            File.WriteAllText(second, "b");
            using var harness = new ActionTestHarness();
            var item = harness.AddToHistory(new ClipboardSnapshot { Files = [first, second] });

            await new ShowInFileManagerAction().ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

            var revealed = Assert.Single(harness.Launcher.RevealedPaths);
            Assert.Equal(first, revealed);
            Assert.Equal(0, harness.Paste.Calls);
            Assert.Empty(harness.Backend.Writes);
            var remaining = Assert.Single(harness.History.Items);
            Assert.Equal(item.Id, remaining.Id);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }
}
