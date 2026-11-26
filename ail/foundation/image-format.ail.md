# Image format

```ail
node ImageFormat : fn   level: system   @existing
  why   An uploaded or saved image keeps its own type.   <- Foundation
  what
    ImageFormat.GetExtension(image: bytes) -> text   — "png", "jpg", "gif", "bmp", "webp" or "tiff"; "png" when unknown
    ImageFormat.GetMediaType(image: bytes) -> text   — "image/png", "image/jpeg" and so on; "image/png" when unknown
  sources
    src/Shapeshifter.Core/Clipboard/ImageFormat.cs
```
