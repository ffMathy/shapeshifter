# Open the link in a QR code

"Open link from QR code" on a copied image that shows a QR code of a web link
opens that link in the browser. Its description shows the link first.

```ail
node OpenQrLink : fn   level: system
  why   QR codes on screen, in a slide, a PDF or a screenshot from a phone,
        hold links meant for a phone camera; on the computer the user had to
        pick up a phone to follow them, then send the link back over.   <- Actions
  what
    OpenQrLinkAction is an IClipboardAction with Id "open-qr-link" and Order 205:
      CanExecute(item) is true when item.Kind is Image and OpenQrLinkAction.FindLink(item.Snapshot.Image) gives a link
      GetTitle(item) is Strings.ActionOpenQrLink, "Open link from QR code"
      GetDescription(item) is Localizer.Format(Strings.ActionOpenQrLinkDescription, OpenQrLinkAction.Shorten(the link)), "Opens {0}"
      ExecuteAsync opens the link with context.Launcher.OpenUrl, and pastes nothing
      the link found for an item is remembered by item.Id, so FindLink runs once per item however often the description is shown
    OpenQrLinkAction.MaxSide: integer = 4096
    OpenQrLinkAction.FindLink(image: bytes or nothing) -> an absolute link or nothing
      gives nothing when SkiaSharp cannot decode the image, or when its longest side is more than MaxSide
      decodes it with SkiaSharp into 32-bit BGRA pixels and searches it for one QR code with ZXing.Net (the ZXing.Net package, which Shapeshifter.Core references), exactly so: a ZXing.QrCode.QRCodeReader decodes a ZXing.BinaryBitmap over a ZXing.Common.HybridBinarizer over a ZXing.RGBLuminanceSource of the pixel bytes, width and height in BitmapFormat.BGRA32, with the hint DecodeHintType.TRY_HARDER set to true; it gives nothing when no QR code is found
      gives the QR code's text, without white space at its start and end, when it is one absolute http or https link with nothing else; otherwise nothing
    OpenQrLinkAction.Shorten(link: an absolute link) -> text
      the link as written; when it is longer than 60 characters, its first 59 characters and then "…"
    in checks, an image with a QR code is made with ZXing's BarcodeWriterPixelData (Format QR_CODE, Width and Height 300, Margin 2), drawn at (450, 250) on a white SkiaSharp bitmap of 1200 × 800, and encoded as PNG
    examples
      FindLink(a PNG of such an image with the QR code of "https://example.com/hello?x=1") => https://example.com/hello?x=1
      FindLink(such an image with the QR code of "http://example.org") => http://example.org/
      FindLink(such an image with the QR code of "WIFI:S:home;T:WPA;P:secret;;") => nothing; not a link
      FindLink(such an image with the QR code of "see https://example.com") => nothing; not only a link
      FindLink(such an image with the QR code of "ftp://example.com/a") => nothing; not http or https
      FindLink(a white PNG of 1200 × 800) => nothing; no QR code
      FindLink(a PNG of 5000 × 10) => nothing; longer than MaxSide
      FindLink([1, 2, 3]) => nothing
      Shorten("https://example.com/a") => "https://example.com/a"
      Shorten("https://example.com/" followed by 60 a's) => its first 59 characters, then "…"
      CanExecute(an Image item of such an image with the QR code of "https://example.com/hello?x=1") => true;  GetDescription => "Opens https://example.com/hello?x=1"
      CanExecute(text "https://example.com") => false
      ExecuteAsync(that Image item) => opens https://example.com/hello?x=1; nothing is pasted, the clipboard and the history are unchanged
  how
    decide the action shows only for an image whose QR code is a web link, and the description shows which
      because the owner asked for the link to be shown before it is opened; a QR code can lead anywhere, and the user should see where before letting go of Ctrl
    decide only http and https links, and only when the code holds nothing else
      because other contents (Wi-Fi passwords, contacts, payments) are not something to open in a browser, and Launcher opens only web and e-mail links
    decide ZXing.Net, with SkiaSharp to decode the image
      because ZXing.Net is the most used barcode reader for .NET, Apache-2.0 licensed and fully managed, and SkiaSharp already ships with the app
    decide images over 4096 pixels on a side are not searched
      because which actions an item offers is worked out when it is copied, and searching a very large image takes too long there; a QR code is found in a 4K screenshot in well under a second
    decide links longer than 60 characters are shortened in the description
      because every description must fit on two lines in every look
  uses      ClipboardAction, ClipboardItem, Launcher, Localization
  sources
    the owner's request in this change: "I2 but where it opens the link in the qr code and shows in the description of the action what the link is"
    ZXing.Net: https://github.com/micjahn/ZXing.Net, package ZXing.Net 0.16.11
```
