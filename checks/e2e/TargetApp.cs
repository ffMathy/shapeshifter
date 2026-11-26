// The other app: one text box that takes commands on stdin and answers each with one line.
using System.Diagnostics;
using System.Text;
using System.Threading.Channels;

namespace Shapeshifter.Checks;

public sealed class TargetApp : IAsyncDisposable
{
    readonly Process process;
    readonly Channel<string> lines = Channel.CreateUnbounded<string>();

    TargetApp(Process process)
    {
        this.process = process;
        _ = Task.Run(async () =>
        {
            // Strip only the line ending: an answer such as "TEXT " (empty text) keeps its space.
            while (await process.StandardOutput.ReadLineAsync() is { } line) lines.Writer.TryWrite(line.TrimEnd('\r'));
            lines.Writer.TryComplete();
        });
    }

    public static async Task<TargetApp> Start()
    {
        var dll = Path.GetFullPath(Path.Combine(Desktop.Root, Environment.GetEnvironmentVariable("AIL_TARGET_APP")
            ?? "tests/Shapeshifter.EndToEnd.TargetApp/bin/Release/net11.0/Shapeshifter.EndToEnd.TargetApp.dll"));
        if (!File.Exists(dll)) throw new FileNotFoundException($"no target app at {dll}; build tests/Shapeshifter.EndToEnd.TargetApp");
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet");
        var info = new ProcessStartInfo(File.Exists(local) ? local : "dotnet")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        info.ArgumentList.Add(dll);
        var process = Process.Start(info)!;
        process.ErrorDataReceived += (_, _) => { };
        process.BeginErrorReadLine();
        var app = new TargetApp(process);
        var ready = await app.Next(60_000);
        if (ready != "TARGET_READY") throw new InvalidOperationException($"the target app said \"{ready}\" instead of TARGET_READY");
        return app;
    }

    async Task<string> Next(int timeout = 10_000)
    {
        using var cts = new CancellationTokenSource(timeout);
        try { return await lines.Reader.ReadAsync(cts.Token); }
        catch (OperationCanceledException) { throw new TimeoutException("the target app did not answer"); }
        catch (ChannelClosedException) { throw new InvalidOperationException("the target app has quit"); }
    }

    public async Task<string> Send(string command)
    {
        await process.StandardInput.WriteLineAsync(command);
        await process.StandardInput.FlushAsync();
        return await Next();
    }

    async Task Ok(string command)
    {
        var answer = await Send(command);
        if (answer != "OK") throw new InvalidOperationException($"\"{command}\" failed: {answer}");
    }

    static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s));

    /// <summary>Copies text in this app, then gives the clipboard manager time to notice.</summary>
    public async Task Copy(string text)
    {
        await Ok($"copy-text {B64(text)}");
        await Desktop.Sleep(800);
    }

    /// <summary>Empties the text box and gives it the keyboard focus.</summary>
    public async Task Prepare()
    {
        await Ok("clear");
        await Ok("focus");
        await Desktop.Sleep(250);
    }

    public async Task<string> Text()
    {
        var answer = await Send("get");
        if (!answer.StartsWith("TEXT ", StringComparison.Ordinal)) throw new InvalidOperationException($"the target app answered \"{answer}\"");
        return Encoding.UTF8.GetString(Convert.FromBase64String(answer[5..]));
    }

    /// <summary>Waits for the text box to show <paramref name="expected"/>, then a moment more, so a stray second paste would show.</summary>
    public async Task<string> WaitForText(string expected, int timeout = 5000)
    {
        var until = DateTime.UtcNow.AddMilliseconds(timeout);
        var text = await Text();
        while (text != expected && DateTime.UtcNow < until) { await Desktop.Sleep(100); text = await Text(); }
        await Desktop.Sleep(500);
        return await Text();
    }

    public async ValueTask DisposeAsync()
    {
        try { await Send("quit"); } catch (Exception) { /* already gone */ }
        try { process.Kill(true); } catch (InvalidOperationException) { /* already gone */ }
        process.Dispose();
    }
}

/// <summary>One target app for all the scenarios of a check, as bun's beforeAll/afterAll did.</summary>
public sealed class TargetAppFixture : IAsyncLifetime
{
    TargetApp? app;

    public TargetApp App => app ?? throw new InvalidOperationException("the target app has not started");

    public async ValueTask InitializeAsync() => app = await TargetApp.Start();

    public async ValueTask DisposeAsync()
    {
        await Desktop.ReleaseAll();
        if (app is not null) await app.DisposeAsync();
    }
}
