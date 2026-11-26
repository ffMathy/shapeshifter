// ail-derived-from: DecodeBase64@337ff818e752eccc
using Shapeshifter.Core.Actions;
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
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82,
    ];

    [Fact(DisplayName = "example 1: TryDecode(\"aGVsbG8gd29ybGQ=\") => \"hello world\"")]
    public void Example1()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("aGVsbG8gd29ybGQ="));
    }

    [Fact(DisplayName = "example 2: TryDecode(\"aGVsbG8gd29ybGQ\") => \"hello world\"; the missing padding is added")]
    public void Example2()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("aGVsbG8gd29ybGQ"));
    }

    [Fact(DisplayName = "example 3: TryDecode(\"  aGVsbG8g\\nd29ybGQ=\\n\") => \"hello world\"; line breaks, as in e-mail, are skipped")]
    public void Example3()
    {
        Assert.Equal("hello world", DecodeBase64Action.TryDecode("  aGVsbG8g\nd29ybGQ=\n"));
    }

    [Fact(DisplayName = "example 4: TryDecode(\"PDw_Pz4-\") => \"<<??>>\"; the URL-safe alphabet")]
    public void Example4()
    {
        Assert.Equal("<<??>>", DecodeBase64Action.TryDecode("PDw_Pz4-"));
    }

    [Fact(DisplayName = "example 5: TryDecode(\"w6bDuMOl\") => \"æøå\"")]
    public void Example5()
    {
        Assert.Equal("æøå", DecodeBase64Action.TryDecode("w6bDuMOl"));
    }

    [Fact(DisplayName = "example 6: TryDecode(\"dGVzdA==\") => \"test\"")]
    public void Example6()
    {
        Assert.Equal("test", DecodeBase64Action.TryDecode("dGVzdA=="));
    }

    [Fact(DisplayName = "example 7: TryDecode(\"dGVzdA\") => nothing; shorter than 8 characters")]
    public void Example7()
    {
        Assert.Null(DecodeBase64Action.TryDecode("dGVzdA"));
    }

    [Fact(DisplayName = "example 8: TryDecode(\"hello world\") => nothing; a space is not Base64")]
    public void Example8()
    {
        Assert.Null(DecodeBase64Action.TryDecode("hello world"));
    }

    [Fact(DisplayName = "example 9: TryDecode(\"username\") => nothing; it decodes, but not to UTF-8")]
    public void Example9()
    {
        Assert.Null(DecodeBase64Action.TryDecode("username"));
    }

    [Fact(DisplayName = "example 10: TryDecode(\"AAECAwQF\") => nothing; bytes 0 to 5 are control characters")]
    public void Example10()
    {
        Assert.Null(DecodeBase64Action.TryDecode("AAECAwQF"));
    }

    [Fact(DisplayName = "example 11: TryDecode(\"aGVsbG8=d29y\") => nothing; = only at the end")]
    public void Example11()
    {
        Assert.Null(DecodeBase64Action.TryDecode("aGVsbG8=d29y"));
    }

    [Fact(DisplayName = "example 12: TryDecode(nothing) => nothing")]
    public void Example12()
    {
        Assert.Null(DecodeBase64Action.TryDecode(null));
    }

    [Fact(DisplayName = "example 13: CanExecute(text \"aGVsbG8gd29ybGQ=\") => true")]
    public void Example13()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "aGVsbG8gd29ybGQ=" });
        var action = new DecodeBase64Action();

        Assert.True(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 14: CanExecute(PNG image) => false")]
    public void Example14()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Image = PngBytes });
        var action = new DecodeBase64Action();

        Assert.False(action.CanExecute(item));
    }

    [Fact(DisplayName = "example 15: ExecuteAsync(text \"aGVsbG8gd29ybGQ=\") => pastes { Text: \"hello world\" }; the history is unchanged")]
    public async Task Example15()
    {
        using var harness = new ActionTestHarness();
        var item = harness.AddToHistory(new ClipboardSnapshot { Text = "aGVsbG8gd29ybGQ=" });
        var action = new DecodeBase64Action();

        await action.ExecuteAsync(item, harness.Context, ActionTestHarness.Ct);

        var written = Assert.Single(harness.Backend.Writes);
        Assert.Equal("hello world", written.Text);
        Assert.Equal(1, harness.Paste.Calls);
        var only = Assert.Single(harness.History.Items);
        Assert.Equal(item.Id, only.Id);
    }
}
