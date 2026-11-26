using System.Diagnostics;
using Serilog;
using Shapeshifter.Core.Platform;

namespace Shapeshifter.Platform.Common;

/// <summary>Opens web and e-mail links, folders and files in the file manager with the desktop's default handler (ShellExecute, <c>open</c> or <c>xdg-open</c>).</summary>
public sealed class Launcher(ILogger logger) : ILauncher
{
    public void OpenUrl(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeMailto)
        {
            throw new ArgumentException("Only web and e-mail links can be opened.", nameof(uri));
        }

        Open(uri.AbsoluteUri);
    }

    public void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Open(path);
    }

    public void RevealInFileManager(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var full = Path.GetFullPath(path);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                // Explorer opens the parent folder with the item selected.
                using var explorer = Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{full}\"") { UseShellExecute = false });
                return;
            }

            if (OperatingSystem.IsMacOS())
            {
                Start("open", ["-R", full]);
                return;
            }

            // Linux: the freedesktop FileManager1 interface selects the item in whichever file manager runs (Files,
            // Dolphin, Nemo, Thunar…); without one, the folder that holds it is opened.
            using var process = Process.Start(new ProcessStartInfo("gdbus")
            {
                ArgumentList =
                {
                    "call", "--session", "--dest", "org.freedesktop.FileManager1", "--object-path", "/org/freedesktop/FileManager1",
                    "--method", "org.freedesktop.FileManager1.ShowItems", $"['{new Uri(full).AbsoluteUri.Replace("'", "%27", StringComparison.Ordinal)}']", string.Empty,
                },
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is not null && process.WaitForExit(TimeSpan.FromSeconds(5)) && process.ExitCode == 0)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.Warning(exception, "Could not show {Path} in the file manager", full);
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
        {
            var folder = Directory.Exists(full) ? Path.GetDirectoryName(full.TrimEnd('/')) : Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(folder))
            {
                Open(folder);
            }
        }
    }

    private static void Start(string file, IEnumerable<string> arguments)
    {
        var info = new ProcessStartInfo(file) { UseShellExecute = false };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info);
    }

    private void Open(string target)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.Warning(exception, "Could not open {Target}", target);
        }
    }
}
