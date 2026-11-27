using Avalonia.Controls;
using Shapeshifter.Core.Localization;
using Shapeshifter.Desktop.Services;

namespace Shapeshifter.Desktop.Views;

/// <summary>
/// The donation notice's card: "Support Shapeshifter", why a monthly donation helps, and the buttons "Donate monthly",
/// "I already donate" and "Not now". It never takes the focus and stays until <see cref="DonationNotifier"/> closes it.
/// </summary>
public partial class DonationWindow : Window
{
    public DonationWindow()
    {
        InitializeComponent();
        if (Localizer.IsRightToLeft)
        {
            FlowDirection = Avalonia.Media.FlowDirection.RightToLeft;
        }

        TitleText.Text = Strings.DonationTitle;
        MessageText.Text = Strings.DonationMessage;
        DonateButton.Content = Strings.DonationDonateMonthly;
        AlreadyButton.Content = Strings.DonationAlreadyDonate;
        LaterButton.Content = Strings.DonationNotNow;

        NativeMaterial.Prepare(this, Chrome);

        // As the copy notification: without a compositor the card has to fill its window.
        Opened += (_, _) => Chrome.Classes.Set("opaque", ActualTransparencyLevel == WindowTransparencyLevel.None);

        DonateButton.Click += (_, _) => DonateClicked?.Invoke(this, EventArgs.Empty);
        AlreadyButton.Click += (_, _) => AlreadyDonateClicked?.Invoke(this, EventArgs.Empty);
        LaterButton.Click += (_, _) => NotNowClicked?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raised when the user clicks "Donate monthly".</summary>
    public event EventHandler? DonateClicked;

    /// <summary>Raised when the user clicks "I already donate".</summary>
    public event EventHandler? AlreadyDonateClicked;

    /// <summary>Raised when the user clicks "Not now".</summary>
    public event EventHandler? NotNowClicked;

    /// <summary>Whether the buttons can be clicked: not while a sign-in or a checkout is under way.</summary>
    public void SetBusy(bool busy) => Buttons.IsEnabled = !busy;
}
