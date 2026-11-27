using Avalonia;
using Serilog;
using Shapeshifter.Core.Ail;
using Shapeshifter.Desktop.Views;
using Sponsorkit;

namespace Shapeshifter.Desktop.Services;

/// <summary>
/// The donation notice (ail/app/donation-notice): once per start, asks Sponsorkit whether the user gives monthly to
/// GitHub Sponsors "ffMathy" or Open Collective "shapeshifterclipboard", and when not, shows <see cref="DonationWindow"/>
/// where the system's notifications appear. A copy from an app store never asks Sponsorkit anything.
/// </summary>
internal sealed class DonationNotifier
{
    /// <summary>How long after the app is ready the notice waits, so it stays out of the way of starting at login.</summary>
    public static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(5);

    /// <summary>Distance from the edges of the working area, as the copy notification's.</summary>
    private const int Margin = 8;

    private readonly Func<SponsorkitClient> createClient;
    private readonly bool isStoreCopy;
    private readonly ILogger logger;
    private SponsorkitClient? client;
    private bool busy;

    /// <param name="createClient">Makes the Sponsorkit client; never called for a store copy.</param>
    /// <param name="isStoreCopy">Whether an app store installed this copy (<c>StoreInstall.IsActive</c>).</param>
    /// <param name="logger">Where a failed check or sign-in is written.</param>
    public DonationNotifier(Func<SponsorkitClient> createClient, bool isStoreCopy, ILogger logger)
    {
        this.createClient = createClient;
        this.isStoreCopy = isStoreCopy;
        this.logger = logger;
    }

    /// <summary>The Sponsorkit client Shapeshifter uses, as <see cref="App"/> makes it.</summary>
    public static SponsorkitClient CreateClient(SponsorkitMock? mock = null) => new(new SponsorkitOptions
    {
        GitHub = "ffMathy",
        OpenCollective = "shapeshifterclipboard",
        AppName = "Shapeshifter",
        // A desktop app shows a window, so the SDK must not take a redirected console as "not interactive".
        Interactive = true,
        Mock = mock,
    });

    /// <summary>The notice's window, once it was shown (for tests that look at it).</summary>
    internal DonationWindow? Window { get; private set; }

    /// <summary>Whether the notice is on screen.</summary>
    public bool IsVisible => Window?.IsVisible == true;

    /// <summary>
    /// Asks Sponsorkit, after <paramref name="delay"/>, and shows the notice when the donation rule says so. Returns
    /// whether it showed. Never throws: a failed check is logged and shows nothing.
    /// </summary>
    public async Task<bool> RunAsync(TimeSpan delay, CancellationToken cancellationToken = default)
    {
        try
        {
            if (isStoreCopy)
            {
                // A copy from an app store was bought: it does not even ask Sponsorkit.
                return false;
            }

            client = createClient();
            if (client.IsDisabled)
            {
                logger.Information("Sponsorkit is switched off (SPONSORKIT_DISABLE): no donation notice");
                return false;
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(true);
            }

            var result = await client.GetFinancialContributionAsync(cancellationToken).ConfigureAwait(true);
            if (!DonationRules.ShowsNotice(isStoreCopy, client.IsDisabled, Donations(result)))
            {
                logger.Information("A monthly donation was found: no donation notice");
                return false;
            }

            Show();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is SponsorkitError or HttpRequestException or IOException or OperationCanceledException)
        {
            // A donor must never be asked because Sponsorkit could not be reached.
            logger.Warning(exception, "Could not check for a monthly donation; no donation notice");
            return false;
        }
    }

    /// <summary>Closes the notice, as "Not now" does.</summary>
    public void Close() => Window?.Hide();

    private static IReadOnlyList<Donation> Donations(ContributionResult result) =>
        [.. result.Contributions.Select(contribution => new Donation(contribution.IsActive, contribution.Frequency))];

    private void Show()
    {
        var window = Window ??= CreateWindow();
        window.Show();
        window.UpdateLayout();
        Position(window);
    }

    private DonationWindow CreateWindow()
    {
        var window = new DonationWindow();
        window.SizeChanged += (_, _) => Position(window);
        window.NotNowClicked += (_, _) => Close();
        window.DonateClicked += async (_, _) => await AnswerAsync(async sponsorkit =>
        {
            await sponsorkit.OpenCheckoutAsync(new CheckoutOptions { Frequency = Frequencies.Monthly }).ConfigureAwait(true);
            return await sponsorkit.GetFinancialContributionAsync(TimeSpan.Zero).ConfigureAwait(true);
        }).ConfigureAwait(true);
        window.AlreadyDonateClicked += async (_, _) => await AnswerAsync(sponsorkit => sponsorkit.SignInAsync()).ConfigureAwait(true);
        return window;
    }

    /// <summary>Runs a sign-in or a checkout and closes the notice when it ends with a monthly donation.</summary>
    private async Task AnswerAsync(Func<SponsorkitClient, Task<ContributionResult>> answer)
    {
        if (busy || client is null || Window is not { } window)
        {
            return;
        }

        busy = true;
        window.SetBusy(true);
        try
        {
            var result = await answer(client).ConfigureAwait(true);
            if (DonationRules.IsMonthlyDonor(Donations(result)))
            {
                Close();
            }
        }
        catch (Exception exception) when (exception is SponsorkitError or HttpRequestException or IOException or OperationCanceledException or InvalidOperationException)
        {
            // Cancelled, timed out or failed: the notice stays as it was.
            logger.Warning(exception, "The sign-in or checkout from the donation notice did not finish");
        }
        finally
        {
            busy = false;
            window.SetBusy(false);
        }
    }

    private static void Position(DonationWindow window)
    {
        var screen = window.Screens.Primary ?? (window.Screens.All.Count > 0 ? window.Screens.All[0] : null);
        if (screen is null)
        {
            return;
        }

        // As the copy notification: on macOS windows are positioned in points, not in backing pixels.
        var scaling = OperatingSystem.IsMacOS() ? window.DesktopScaling : screen.Scaling;
        var placement = PlatformLook.Resource(Application.Current!, "ToastPlacement", "BottomRight");
        window.Position = CopyNotifier.Place(screen.WorkingArea, window.Bounds.Size * scaling, (int)(Margin * scaling), placement);
    }
}
