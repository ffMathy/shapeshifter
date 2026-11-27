using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that opens the web link held by a QR code in a copied image, so the user need not pick up a phone to follow it.
/// </summary>
public sealed class OpenQrLinkAction : IClipboardAction
{
    /// <summary>
    /// The longest side, in pixels, of an image that is searched for a QR code.
    /// </summary>
    public const int MaxSide = 4096;

    private const int MaxShownLength = 60;

    private readonly ConcurrentDictionary<object, Uri?> links = new();

    /// <summary>
    /// The action's stable identifier.
    /// </summary>
    public string Id => "open-qr-link";

    /// <summary>
    /// The action's position among the actions.
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
    /// Gives the action's description, which shows the link that will be opened.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var link = GetLink(item);
        return link is null
            ? string.Empty
            : Localizer.Format(Strings.ActionOpenQrLinkDescription, Shorten(link));
    }

    /// <summary>
    /// Tells whether the item is an image whose QR code holds a web link.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can open a link for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return GetLink(item) is not null;
    }

    /// <summary>
    /// Opens the link found in the item's QR code, and pastes nothing.
    /// </summary>
    /// <param name="item">The item whose link to open.</param>
    /// <param name="context">The context giving the launcher.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes when the link has been handed to the launcher.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var link = GetLink(item);
        if (link is not null)
        {
            context.Launcher.OpenUrl(link);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Searches an image for one QR code that holds a single absolute http or https link.
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

        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
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
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                return null;
            }
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? uri : null;
    }

    /// <summary>
    /// Shortens a link so it fits in a description.
    /// </summary>
    /// <param name="link">The link to shorten.</param>
    /// <returns>The link as written, or its first 59 characters and an ellipsis when it is longer than 60 characters.</returns>
    public static string Shorten(Uri link)
    {
        ArgumentNullException.ThrowIfNull(link);
        var text = link.OriginalString;
        return text.Length > MaxShownLength ? string.Concat(text.AsSpan(0, MaxShownLength - 1), "…") : text;
    }

    private Uri? GetLink(ClipboardItem item)
    {
        if (item.Kind != ClipboardContentKind.Image)
        {
            return null;
        }

        return links.GetOrAdd(item.Id, _ => FindLink(item.Snapshot.Image));
    }
}
