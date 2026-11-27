using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// Opens the web link held by a QR code in a copied image, so the user does not need a phone to follow it.
/// </summary>
public sealed class OpenQrLinkAction : IClipboardAction
{
    /// <summary>
    /// The longest side, in pixels, of an image that is searched for a QR code.
    /// </summary>
    public const int MaxSide = 4096;

    private readonly ConcurrentDictionary<object, Uri?> _links = new();

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "open-qr-link";

    /// <summary>
    /// The action's position among the actions.
    /// </summary>
    public int Order => 205;

    /// <summary>
    /// Gets the action's title.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionOpenQrLink;
    }

    /// <summary>
    /// Gets the action's description, which shows the link that will be opened.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description naming the link.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var link = LinkOf(item) ?? throw new InvalidOperationException("The item holds no QR code with a web link.");
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
        return item.Kind == ClipboardContentKind.Image && LinkOf(item) is not null;
    }

    /// <summary>
    /// Opens the item's QR code link with the system's default app; nothing is pasted.
    /// </summary>
    /// <param name="item">The item whose link to open.</param>
    /// <param name="context">The context giving access to the launcher.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes when the link has been handed to the system.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var link = LinkOf(item) ?? throw new InvalidOperationException("The item holds no QR code with a web link.");
        context.Launcher.OpenUrl(link);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the http or https link held by the single QR code in an image.
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

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, codec.Info.AlphaType);
        using var bitmap = SKBitmap.Decode(codec, info);
        if (bitmap is null)
        {
            return null;
        }

        var source = new ZXing.RGBLuminanceSource(bitmap.Bytes, bitmap.Width, bitmap.Height, ZXing.RGBLuminanceSource.BitmapFormat.BGRA32);
        var binary = new ZXing.BinaryBitmap(new ZXing.Common.HybridBinarizer(source));
        var hints = new Dictionary<ZXing.DecodeHintType, object> { [ZXing.DecodeHintType.TRY_HARDER] = true };
        var result = new ZXing.QrCode.QRCodeReader().decode(binary, hints);
        if (result?.Text is null)
        {
            return null;
        }

        var text = result.Text.Trim();
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
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
    /// Shortens a link to at most 60 characters for display.
    /// </summary>
    /// <param name="link">The link to shorten.</param>
    /// <returns>The link as written, or its first 59 characters followed by an ellipsis.</returns>
    public static string Shorten(Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var written = link.OriginalString;
        return written.Length > 60 ? string.Concat(written.AsSpan(0, 59), "…") : written;
    }

    private Uri? LinkOf(ClipboardItem item) =>
        _links.GetOrAdd(item.Id, static (_, image) => FindLink(image), item.Snapshot.Image);
}
