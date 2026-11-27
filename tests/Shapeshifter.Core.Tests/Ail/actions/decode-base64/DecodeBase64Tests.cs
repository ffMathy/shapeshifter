// ail-derived-from: DecodeBase64@088c1e435c783c70
using Shapeshifter.Core.Ail;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Tests.Actions;
using Xunit;

namespace Shapeshifter.Core.Ail.Tests;

/// <summary>Derived checks for the DecodeBase64 node's examples.</summary>
public sealed class DecodeBase64Tests
{
    private static readonly byte[] PngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89,
    ];

    [Fact(DisplayName = "example 1: TryDecode(\"aGVsbG8gd29ybGQ=\") => \"hello world\"")]
    public void Example01()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("aGVsbG8gd29ybGQ="));
    }

    [Fact(DisplayName = "example 2: TryDecode(\"aGVsbG8gd29ybGQ\") => \"hello world\"; the missing padding is added")]
    public void Example02()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("aGVsbG8gd29ybGQ"));
    }

    [Fact(DisplayName = "example 3: TryDecode(\"  aGVsbG8g\\nd29ybGQ=\\n\") => \"hello world\"; line breaks, as in e-mail, are skipped")]
    public void Example03()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("  aGVsbG8g\nd29ybGQ=\n"));
    }

    [Fact(DisplayName = "example 4: TryDecode(\"PDw_Pz4-\") => \"<<??>>\"; the URL-safe alphabet")]
    public void Example04()
    {
        Assert.Equal("<<??>>", DecodeBase64Action.TryDecode("PDw_Pz4-"));
    }

    [Fact(DisplayName = "example 5: TryDecode(\"w6bDuMOl\") => \"æøå\"")]
    public void Example05()
    {
        Assert.Equal("æøå", DecodeBase64Action.TryDecode("w6bDuMOl"));
    }

    [Fact(DisplayName = "example 6: TryDecode(\"dGVzdA==\") => \"test\"")]
    public void Example06()
    {
        Assert.Equal("test", DecodeBase64Action.TryDecode("dGVzdA=="));
    }

    [Fact(DisplayName = "example 7: TryDecode(\"dGVzdA\") => nothing; shorter than 8 characters")]
    public void Example07()
    {
        Assert.Null(DecodeBase64Action.TryDecode("dGVzdA"));
    }

    [Fact(DisplayName = "example 8: TryDecode(\"hello world\") => nothing; a space is not Base64")]
    public void Example08()
    {
        Assert.Null(DecodeBase64Action.TryDecode("hello world"));
    }

    [Fact(DisplayName = "example 9: TryDecode(\"username\") => nothing; it decodes, but not to UTF-8")]
    public void Example09()
    {
        Assert.Null(DecodeBase64Action.TryDecode("username"));
    }

    [Fact(DisplayName = "example 10: TryDecode(\"1795703400\") => nothing; a Unix time, whose padded form is not valid Base64")]
    public void Example10()
    {
        Assert.Null(DecodeBase64Action.TryDecode("1795703400"));
    }

    [Fact(DisplayName = "example 11: TryDecode(\"AAECAwQF\") => nothing; bytes 0 to 5 are control characters")]
    public void Example11()
    {
        Assert.Null(DecodeBase64Action.TryDecode("AAECAwQF"));
    }

    [Fact(DisplayName = "example 12: TryDecode(\"aGVsbG8=d29y\") => nothing; = only at the end")]
    public void Example12()
    {
        Assert.Null(DecodeBase64Action.TryDecode("aGVsbG8=d29y"));
    }

    [Fact(DisplayName = "example 13: TryDecode(nothing) => nothing")]
    public void Example13()
    {
        Assert.Null(DecodeBase64Action.TryDecode(null));
    }

    [Fact(DisplayName = "example 14: CanExecute(text \"aGVsbG8gd29ybGQ=\") => true")]
    public void Example14()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "aGVsbG8gd29ybGQ=" });
        var action = new DecodeBase64Action();

        Assert.True(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 15: CanExecute(PNG image) => false")]
    public void Example15()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });
        var action = new DecodeBase64Action();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 16: ExecuteAsync(text \"aGVsbG8gd29ybGQ=\") => pastes { Text: \"hello world\" }; the history is unchanged")]
    public async Task Example16()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "aGVsbG8gd29ybGQ=" });
        var action = new DecodeBase64Action();
        var historyBefore = harness.History.Items.Count;

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("hello world", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        Assert.Equal(historyBefore, harness.History.Items.Count);
        Assert.Equal(item.Id, harness.History.Items[0].Id);
    }
}
