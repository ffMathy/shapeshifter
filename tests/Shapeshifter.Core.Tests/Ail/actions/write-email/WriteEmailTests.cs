// ail-derived-from: WriteEmail@2e8bb21b30c47bec
using System.Globalization;
using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the WriteEmail node: its examples, as written in the spec.</summary>
public sealed class WriteEmailTests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
    ];

    [Fact(DisplayName = "example 1: ParseAddress(\"anna@example.com\") => \"anna@example.com\"")]
    public void Example01()
    {
        Assert.Equal("anna@example.com", WriteEmailAction.ParseAddress("anna@example.com"));
    }

    [Fact(DisplayName = "example 2: ParseAddress(\"  mailto:Anna.Berg+news@mail.example.co.uk \") => \"Anna.Berg+news@mail.example.co.uk\"")]
    public void Example02()
    {
        Assert.Equal(
            "Anna.Berg+news@mail.example.co.uk",
            WriteEmailAction.ParseAddress("  mailto:Anna.Berg+news@mail.example.co.uk "));
    }

    [Fact(DisplayName = "example 3: ParseAddress(\"søren@eksempel.dk\") => \"søren@eksempel.dk\"")]
    public void Example03()
    {
        Assert.Equal("søren@eksempel.dk", WriteEmailAction.ParseAddress("søren@eksempel.dk"));
    }

    [Fact(DisplayName = "example 4: ParseAddress(\"anna@localhost\") => nothing")]
    public void Example04()
    {
        Assert.Null(WriteEmailAction.ParseAddress("anna@localhost"));
    }

    [Fact(DisplayName = "example 5: ParseAddress(\"anna@example.c\") => nothing")]
    public void Example05()
    {
        Assert.Null(WriteEmailAction.ParseAddress("anna@example.c"));
    }

    [Fact(DisplayName = "example 6: ParseAddress(\"anna@@example.com\") => nothing")]
    public void Example06()
    {
        Assert.Null(WriteEmailAction.ParseAddress("anna@@example.com"));
    }

    [Fact(DisplayName = "example 7: ParseAddress(\".anna@example.com\") => nothing")]
    public void Example07()
    {
        Assert.Null(WriteEmailAction.ParseAddress(".anna@example.com"));
    }

    [Fact(DisplayName = "example 8: ParseAddress(\"write to anna@example.com\") => nothing")]
    public void Example08()
    {
        Assert.Null(WriteEmailAction.ParseAddress("write to anna@example.com"));
    }

    [Fact(DisplayName = "example 9: ParseAddress(\"anna@example.com, bo@example.com\") => nothing")]
    public void Example09()
    {
        Assert.Null(WriteEmailAction.ParseAddress("anna@example.com, bo@example.com"));
    }

    [Fact(DisplayName = "example 10: CanExecute(text \"anna@example.com\") => true;  GetDescription => \"Opens a new email to anna@example.com.\"")]
    public void Example10()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "anna@example.com" });
        var action = new WriteEmailAction();

        Assert.True(action.CanExecute(item));

        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            Assert.Equal("Opens a new email to anna@example.com.", action.GetDescription(item));
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    [Fact(DisplayName = "example 11: CanExecute(PNG image) => false")]
    public void Example11()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });
        var action = new WriteEmailAction();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 12: ExecuteAsync(text \"anna@example.com\") => opens mailto:anna@example.com; nothing is pasted, the clipboard and the history are unchanged")]
    public async Task Example12()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "anna@example.com" });
        var historyBefore = harness.History.Items.ToList();
        var action = new WriteEmailAction();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var opened = Assert.Single(harness.Launcher.OpenedUrls);
        Assert.Equal(new Uri("mailto:anna@example.com"), opened);
        Assert.Equal(0, harness.Paste.Calls);
        Assert.Empty(harness.Backend.Writes);
        Assert.Equal(historyBefore, harness.History.Items.ToList());
    }
}
