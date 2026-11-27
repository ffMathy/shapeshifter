// ail-derived-from: PreviewCard@b5d9d5592b12af5d
using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the preview card: the larger view of the chosen item, above its actions.</summary>
public sealed class PreviewCardTests
{
    /// <summary>The eight bytes every PNG file starts with.</summary>
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>The CRC-32 lookup table PNG chunks are checked with.</summary>
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>Whether the window shows a loaded picture wider than a row's tile, as the preview card's does.</summary>
    private static bool ShowsLargeImage(Window window)
    {
        foreach (var image in AppHarness.Find<Image>(window))
        {
            if (image.Source is not null && image.Bounds.Width > 100)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Builds the CRC-32 lookup table.</summary>
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    /// <summary>Computes the CRC-32 of a chunk's type and data.</summary>
    private static uint Crc(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        foreach (var b in data)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFFu;
    }

    /// <summary>Writes one PNG chunk to the stream.</summary>
    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        var length = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        stream.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);
        var crc = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(typeBytes, data));
        stream.Write(crc);
    }

    /// <summary>Writes a 320 × 200 PNG picture to the given path.</summary>
    private static void WritePng(string path)
    {
        const int width = 320;
        const int height = 200;
        var raw = new byte[height * (1 + (width * 3))];
        var i = 0;
        for (var y = 0; y < height; y++)
        {
            raw[i++] = 0;
            var green = (byte)(200 - (y * 150 / height));
            var blue = (byte)(60 + (y * 100 / height));
            for (var x = 0; x < width; x++)
            {
                raw[i++] = 255;
                raw[i++] = green;
                raw[i++] = blue;
            }
        }

        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var zlib = new ZLibStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(raw);
            }

            compressed = buffer.ToArray();
        }

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 2;

        using var file = File.Create(path);
        file.Write(PngSignature);
        WriteChunk(file, "IHDR", header);
        WriteChunk(file, "IDAT", compressed);
        WriteChunk(file, "IEND", []);
    }

    [Fact(DisplayName = "code")]
    public Task Code() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("28 characters"), "the strip's caption");
        var texts = AppHarness.Texts(app.Picker);
        Assert.Contains("dotnet run --project src/App", texts);
        Assert.Contains("28 characters", texts);

        AppHarness.SaveScreen(app.Picker, "code.png");
    });

    [Fact(DisplayName = "text of several lines")]
    public Task TextOfSeveralLines() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("line one of three\nline two of three\nline three of three", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("3 lines · 55 characters"), "the strip's caption");
        var texts = AppHarness.Texts(app.Picker);
        Assert.Contains("line one of three\nline two of three\nline three of three", texts);
        Assert.Contains("3 lines · 55 characters", texts);

        AppHarness.SaveScreen(app.Picker, "text-of-several-lines.png");
    });

    [Fact(DisplayName = "a colour")]
    public Task AColour() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("#2c5497", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.True(app.HistoryRows[0].IsSelected);
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("rgb(44, 84, 151)"), "the strip's caption");
        var texts = AppHarness.Texts(app.Picker);
        Assert.Contains("#2C5497", texts);
        Assert.Contains("rgb(44, 84, 151)", texts);

        AppHarness.SaveScreen(app.Picker, "a-colour.png");
    });

    [Fact(DisplayName = "an image")]
    public Task AnImage() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(320, 200);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        Assert.True(app.HistoryRows[0].IsSelected);
        await AppHarness.WaitUntilAsync(() => ShowsLargeImage(app.Picker), "the card's picture");
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("Image · 320 × 200"), "the strip's caption");
        Assert.Contains("Image · 320 × 200", AppHarness.Texts(app.Picker));

        AppHarness.SaveScreen(app.Picker, "an-image.png");
    });

    [Fact(DisplayName = "files")]
    public Task Files() => AppHarness.RunAsync(async app =>
    {
        Directory.CreateDirectory("/tmp/files");
        await File.WriteAllTextAsync("/tmp/files/notes.txt", "notes");
        await File.WriteAllTextAsync("/tmp/files/report.pdf", "report");
        await app.CopyFilesAsync(["/tmp/files/notes.txt", "/tmp/files/report.pdf"]);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        await AppHarness.WaitUntilAsync(
            () => AppHarness.Texts(app.Picker).Contains("2 files"), "the strip's caption");
        var texts = AppHarness.Texts(app.Picker);
        Assert.Contains("notes.txt", texts);
        Assert.Contains("report.pdf", texts);
        Assert.Contains("2 files", texts);

        AppHarness.SaveScreen(app.Picker, "files.png");
    });

    [Fact(DisplayName = "one file, on Linux")]
    public Task OneFileOnLinux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This scenario applies on Linux only.");
        return AppHarness.RunAsync(async app =>
        {
            Directory.CreateDirectory("/tmp/files");
            await File.WriteAllTextAsync("/tmp/files/notes.txt", "notes");
            await app.CopyFilesAsync(["/tmp/files/notes.txt"]);

            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            await AppHarness.WaitUntilAsync(
                () => AppHarness.Texts(app.Picker).Contains("/tmp/files"), "the strip's folder");
            var texts = AppHarness.Texts(app.Picker);
            Assert.Contains("notes.txt", texts);
            Assert.Contains("/tmp/files", texts);

            AppHarness.SaveScreen(app.Picker, "one-file-on-linux.png");
        }, new AppOptions { Look = Look.Linux });
    }

    [Fact(DisplayName = "the path of an image, on Linux")]
    public Task ThePathOfAnImageOnLinux()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "This scenario applies on Linux only.");
        return AppHarness.RunAsync(async app =>
        {
            Directory.CreateDirectory("/tmp/pictures");
            WritePng("/tmp/pictures/sunset.png");
            await app.CopyAsync("/tmp/pictures/sunset.png", null);

            await app.HoldCtrlVAsync();

            Assert.True(app.PickerVisible);
            Assert.True(app.HistoryRows[0].IsSelected);
            await AppHarness.WaitUntilAsync(() => ShowsLargeImage(app.Picker), "the card's picture");
            await AppHarness.WaitUntilAsync(
                () => AppHarness.Texts(app.Picker).Contains("/tmp/pictures"), "the strip's folder");
            var texts = AppHarness.Texts(app.Picker);
            Assert.Contains("/tmp/pictures/sunset.png", texts);
            Assert.Contains("/tmp/pictures", texts);

            AppHarness.SaveScreen(app.Picker, "the-path-of-an-image-on-linux.png");
        }, new AppOptions { Look = Look.Linux });
    }

    [Fact(DisplayName = "a search that finds nothing")]
    public Task ASearchThatFindsNothing() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("hello world", null);

        await app.HoldCtrlVAsync();
        await app.TypeSearchAsync("zzqq");

        Assert.True(app.PickerVisible);
        await AppHarness.WaitUntilAsync(() => app.HistoryRows.Count == 0, "no results");
        var texts = AppHarness.Texts(app.Picker);
        Assert.DoesNotContain("hello world", texts);
        Assert.DoesNotContain("11 characters", texts);

        AppHarness.SaveScreen(app.Picker, "a-search-that-finds-nothing.png");
    });
}
