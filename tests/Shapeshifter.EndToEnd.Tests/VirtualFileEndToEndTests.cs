using SharpHook.Data;

namespace Shapeshifter.EndToEnd.Tests;

/// <summary>
/// Issue #171: files copied in a Remote Desktop session reach the local clipboard as virtual files, whose contents
/// are streams the session serves only while its copy is on the clipboard. Picked from the history after something
/// else was copied, such a file must paste as a real file with the same contents.
/// </summary>
[Collection(nameof(EndToEndCollection))]
public sealed class VirtualFileEndToEndTests(ShapeshifterSession session)
{
    [Fact]
    public async Task AFileCopiedInARemoteDesktopSessionPastesAsAFile()
    {
        if (!ShapeshifterSession.IsEnabled)
        {
            Assert.Skip(ShapeshifterSession.SkipReason);
        }

        if (!OperatingSystem.IsWindows())
        {
            Assert.Skip("Virtual files (FileGroupDescriptorW and FileContents) are a Windows clipboard format.");
        }

        var id = Guid.NewGuid().ToString("N")[..6];
        var name = $"report-{id}.txt";
        var contents = $"remote {id}";
        await session.CopyTextAsync($"before {id}");
        var captured = await session.CopyVirtualFileAsync(name, contents);
        Assert.True(captured.Kinds[0] == "File", $"The copy was captured as {captured.Kinds[0]} ('{captured.Previews[0]}'), not as a file.\n{session.Logs}");

        // Copying something else takes the virtual files off the clipboard, so their streams can no longer be read.
        await session.CopyTextAsync($"after {id}");
        await session.PrepareTargetAsync();
        await session.PressModifierAsync();
        await session.HoldVUntilThePickerShowsAsync();
        await session.TapAsync(KeyCode.VcV);
        await session.WaitForStateAsync(state => state.PickerVisible && state.SelectedIndex == 1, "file item selected");
        await session.ReleaseModifierAsync();
        await session.WaitForStateAsync(state => !state.PickerVisible && state.Kinds[0] == "File", "file item pasted and moved to the top");
        await session.WaitForIdleAsync();

        var files = await session.ReadFilesAsync();
        var report = $"Clipboard files: {string.Join(", ", files.Select(file => $"{file.Name} = '{file.Contents}'"))}\n"
            + $"Clipboard formats: {string.Join(", ", await session.GetClipboardFormatsAsync())}\n{session.Logs}";
        Assert.True(files.Count == 1 && files[0].Name == name && files[0].Contents == contents, report);
    }
}
