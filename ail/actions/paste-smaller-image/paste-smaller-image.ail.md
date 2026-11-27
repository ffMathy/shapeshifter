# Paste a smaller image

"Paste smaller image" pastes a large copied image scaled down, so that its
longest side is at most 1920 pixels.

```ail
node PasteSmallerImage : fn   level: system
  why   Screenshots of large screens and photos are often too big for a chat,
        a ticket or a form that limits the size, and slow to send; scaling
        one down meant an image editor.   <- Actions
  what
    PasteSmallerImageAction is an IClipboardAction with Id "paste-smaller-image" and Order 60:
      CanExecute(item) is true when item.Kind is Image and PasteSmallerImageAction.Measure(item.Snapshot.Image) gives a size whose longest side is more than MaxSide
      GetTitle(item) is Strings.ActionPasteSmallerImage, "Paste smaller image"
      GetDescription(item) is Localizer.Format(Strings.ActionPasteSmallerImageDescription, the new width, the new height), "Pastes this image scaled down to {0} × {1} pixels."
      ExecuteAsync pastes ClipboardSnapshot { Image: PasteSmallerImageAction.Shrink(item.Snapshot.Image) } with context.WriteAndPasteAsync; nothing is added to the history
    PasteSmallerImageAction.MaxSide: integer = 1920
    PasteSmallerImageAction.Measure(image: bytes or nothing) -> { Width: integer, Height: integer } or nothing
      reads only the image's header with SkiaSharp (SKCodec.Create on the bytes, then its Info), without decoding the pixels; nothing when SkiaSharp cannot read the image
    PasteSmallerImageAction.NewSize(width: integer, height: integer) -> { Width: integer, Height: integer }
      the longest side becomes MaxSide, the other is scaled by the same factor and rounded to the nearest whole number, halves up, and never below 1
    PasteSmallerImageAction.Shrink(image: bytes) -> bytes
      decodes the image with SkiaSharp, scales it to NewSize with cubic resampling (Mitchell), and encodes it in its own type: a JPEG as JPEG at quality 85, anything else as PNG
      what an image's EXIF says about its orientation is applied before scaling, so the result shows the right way up
    the libraries: SkiaSharp 3.119.4 (the version Avalonia already ships) and SkiaSharp.NativeAssets.Linux, which Shapeshifter.Core references
    examples
      NewSize(3840, 2160) => { 1920, 1080 }
      NewSize(1000, 4000) => { 480, 1920 }
      NewSize(2000, 3) => { 1920, 3 }
      NewSize(5000, 1) => { 1920, 1 }; never below 1
      Measure(a PNG of 2560 × 1440) => { 2560, 1440 }
      Measure([1, 2, 3]) => nothing
      CanExecute(a PNG image of 2560 × 1440) => true;  GetDescription => "Pastes this image scaled down to 1920 × 1080 pixels."
      CanExecute(a PNG image of 1920 × 1200) => false; not larger than MaxSide
      CanExecute(image bytes SkiaSharp cannot read) => false
      CanExecute(text "hello") => false
      Shrink(a PNG of 2560 × 1440) => a PNG of 1920 × 1080
      Shrink(a JPEG of 4000 × 3000) => a JPEG of 1920 × 1440
      ExecuteAsync(a PNG image of 2560 × 1440) => pastes { Image: a PNG of 1920 × 1080 }; the history is unchanged
  how
    decide at most 1920 pixels on the longest side
      because that is a full HD screen, sharp on almost every display, and keeps most screenshots and photos under the upload limits of chats and forms
    decide offered only for an image larger than that, measured from its header
      because a smaller image would paste unchanged, and which actions an item offers is worked out when it is copied, where decoding a large image would be slow
    decide JPEG stays JPEG at quality 85, everything else becomes PNG
      because photos are JPEGs and PNG would make them larger, while screenshots with sharp text and transparency need PNG
    decide SkiaSharp
      because the app already ships it with Avalonia, so it adds no size and no new native library
    decide the description gives the new size
      because the user sees what will be pasted before letting go of Ctrl (#62)
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "I3 Paste smaller image"
```
