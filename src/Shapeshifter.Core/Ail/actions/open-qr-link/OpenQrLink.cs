using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that opens the web link held by a QR code in a copied image.
/// </summary>
public sealed class OpenQrLinkAction : IClipboardAction
{
    /// <summary>
    /// The longest side, in pixels, of an image that is searched for a QR code.
    /// </summary>
    public const int MaxSide = 4096;

    private const int MaxDescriptionLinkLength = 60;

    private readonly Lock _gate = new();
    private readonly Dictionary<object, Uri?> _links = [];

    /// <summary>
    /// The action's identifier.
    /// </summary>
    public string Id => "open-qr-link";

    /// <summary>
    /// Where the action sorts among the actions.
    /// </summary>
    public int Order => 205;

    /// <summary>
    /// Gives the action's title.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionOpenQrLink;
    }

    /// <summary>
    /// Gives the action's description, naming the link it opens.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var link = LinkFor(item) ?? throw new InvalidOperationException("The item holds no QR code link.");
        return Localizer.Format(Strings.ActionOpenQrLinkDescription, Shorten(link));
    }

    /// <summary>
    /// Tells whether the item is an image whose QR code holds a web link.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action applies to the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Image && LinkFor(item) is not null;
    }

    /// <summary>
    /// Opens the link from the item's QR code, pasting nothing.
    /// </summary>
    /// <param name="item">The item whose link is opened.</param>
    /// <param name="context">The context giving the launcher.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the link has been handed to the launcher.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var link = LinkFor(item) ?? throw new InvalidOperationException("The item holds no QR code link.");
        context.Launcher.OpenUrl(link);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the absolute http or https link held by the one QR code in an image.
    /// </summary>
    /// <param name="image">The encoded image, or null.</param>
    /// <returns>The link, or null when there is none.</returns>
    public static Uri? FindLink(byte[]? image)
    {
        if (image is null || image.Length == 0)
        {
            return null;
        }

        using var data = SKData.CreateCopy(image);
        if (data is null)
        {
            return null;
        }

        using var codec = SKCodec.Create(data);
        if (codec is null)
        {
            return null;
        }

        var width = codec.Info.Width;
        var height = codec.Info.Height;
        if (width <= 0 || height <= 0 || Math.Max(width, height) > MaxSide)
        {
            return null;
        }

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var bitmap = SKBitmap.Decode(codec, info);
        if (bitmap is null)
        {
            return null;
        }

        var source = new RGBLuminanceSource(bitmap.Bytes, bitmap.Width, bitmap.Height, RGBLuminanceSource.BitmapFormat.BGRA32);
        var binaryBitmap = new BinaryBitmap(new HybridBinarizer(source));
        var hints = new Dictionary<DecodeHintType, object> { [DecodeHintType.TRY_HARDER] = true };
        var result = new QRCodeReader().decode(binaryBitmap, hints);
        if (result?.Text is null)
        {
            return null;
        }

        var text = result.Text.Trim();
        if (text.Length == 0)
        {
            return null;
        }

        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                return null;
            }
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var link))
        {
            return null;
        }

        return link.Scheme == Uri.UriSchemeHttp || link.Scheme == Uri.UriSchemeHttps ? link : null;
    }

    /// <summary>
    /// Shortens a link for showing in the description.
    /// </summary>
    /// <param name="link">The link to shorten.</param>
    /// <returns>The link as written, or its first 59 characters and an ellipsis when it is longer than 60 characters.</returns>
    public static string Shorten(Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var text = link.OriginalString;
        return text.Length > MaxDescriptionLinkLength
            ? string.Concat(text.AsSpan(0, MaxDescriptionLinkLength - 1), "…")
            : text;
    }

    private Uri? LinkFor(ClipboardItem item)
    {
        lock (_gate)
        {
            if (_links.TryGetValue(item.Id, out var known))
            {
                return known;
            }

            var link = FindLink(item.Snapshot.Image);
            _links[item.Id] = link;
            return link;
        }
    }
}
