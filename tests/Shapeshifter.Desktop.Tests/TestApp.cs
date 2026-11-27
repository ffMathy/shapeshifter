using System.Globalization;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

namespace Shapeshifter.Desktop.Tests;

/// <summary>
/// The real application on Avalonia's headless platform, rendered with Skia, set up once on a thread of its own that
/// becomes its UI thread and runs its dispatcher. Tests hand their work to that thread with <see cref="RunAsync(Action)"/>
/// or, when they await, <see cref="RunAsync(Func{Task})"/>.
/// </summary>
internal static class TestApp
{
    private static readonly Task<Dispatcher> Started = Start();

    /// <summary>
    /// One test at a time: the look, the theme and the display language belong to the one application, and test classes
    /// run in parallel, so a test would otherwise see another's look or language half-way through.
    /// </summary>
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    public static Task RunAsync(Action test) => RunAsync(() =>
    {
        test();
        return Task.CompletedTask;
    });

    public static async Task RunAsync(Func<Task> test)
    {
        var dispatcher = await Started.ConfigureAwait(false);
        await OneAtATime.WaitAsync().ConfigureAwait(false);
        var culture = CultureInfo.CurrentUICulture;
        try
        {
            await dispatcher.InvokeAsync(test);
        }
        finally
        {
            // Back to the language the run started in, whatever the test chose.
            await dispatcher.InvokeAsync(() =>
            {
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = null;
                Shapeshifter.Core.Localization.Strings.Culture = null;
            });
            OneAtATime.Release();
        }
    }

    private static Task<Dispatcher> Start()
    {
        var started = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                // Skia and the bundled Inter font, as the app uses, so that text is shaped and measured as it is shown.
                AppBuilder.Configure<App>()
                    .UseSkia()
                    .WithInterFont()
                    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                    .SetupWithoutStarting();
            }
            catch (Exception exception)
            {
                started.SetException(exception);
                return;
            }

            started.SetResult(Dispatcher.UIThread);
            Dispatcher.UIThread.MainLoop(CancellationToken.None);
        })
        {
            IsBackground = true,
            Name = "Avalonia UI thread",
        };
        thread.Start();
        return started.Task;
    }
}
