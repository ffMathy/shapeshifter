# Paste without location

"Paste without location" pastes a copied photo without the metadata that
says where it was taken.

```ail
node PasteWithoutLocation : fn   level: system
  why   Phones write the place a photo was taken into the photo itself. Sent
        on as it is, it tells strangers where someone lives or works, and
        removing it needs a tool few people have.   <- Actions
  what
    PasteWithoutLocationAction is an IClipboardAction with Id "paste-without-location" and Order 62:
      CanExecute(item) is true when the item's image bytes, read as below, are a JPEG, PNG or WebP and PasteWithoutLocationAction.HasLocation(them)
        for an Image item the bytes are item.Snapshot.Image; for a File item, the one path in item.Snapshot.Files when it is not a network path (starting \\ or //), names an existing file of at most 25 MB, and is a JPEG, PNG or WebP by its first bytes
      GetTitle(item) is Strings.ActionPasteWithoutLocation, "Paste without location"
      GetDescription(item) is Strings.ActionPasteWithoutLocationDescription, "Pastes this photo without the place it was taken and its other metadata."
      ExecuteAsync: for an Image item, pastes ClipboardSnapshot { Image: Clean(the bytes) }; for a File item, writes Clean(the file's bytes) to <context.Paths.TempDirectory>/Without location/<8 random hexadecimal digits>/<the file's own name> and pastes ClipboardSnapshot { Files: [that path] }; both with context.WriteAndPasteAsync, and nothing is added to the history
    PasteWithoutLocationAction.HasLocation(image: bytes) -> boolean
      true when MetadataExtractor (the MetadataExtractor package, which Shapeshifter.Core references) finds a GpsDirectory in the image whose GetGeoLocation() gives a position; any exception while reading gives false
    PasteWithoutLocationAction.Clean(image: bytes) -> bytes
      decodes the image with SkiaSharp, turned the way its EXIF orientation says, and encodes it again in its own type: JPEG at quality 95, PNG, or WebP at quality 95
      SkiaSharp writes no metadata, so the result holds no EXIF, XMP or IPTC: no position, no camera, no time
    the fixture tests/Shapeshifter.Core.Tests/Fixtures/photo-with-location.jpg, which the test project copies next to the tests, is a 40 × 30 JPEG whose EXIF holds orientation 1 and the position 55.6761 N, 12.5683 E
    examples
      HasLocation(the fixture's bytes) => true
      HasLocation(a PNG made by SkiaSharp) => false
      HasLocation([1, 2, 3]) => false
      Clean(the fixture's bytes) => a JPEG of 40 × 30 for which HasLocation is false
      CanExecute(an Image item of the fixture's bytes) => true
      CanExecute(files [a copy of the fixture]) => true
      CanExecute(files [a PNG file without metadata]) => false
      CanExecute(text "hello") => false
      ExecuteAsync(an Image item of the fixture's bytes) => pastes { Image: a JPEG without a position }; the history is unchanged
      ExecuteAsync(files ["…/holiday.jpg", a copy of the fixture]) => pastes { Files: ["<temp>/Without location/<8 hex>/holiday.jpg"] }, a JPEG without a position; the original file is unchanged
  how
    decide all metadata is removed, not only the position
      because the time, the camera and its serial number can identify a person too, and keeping some fields would need rewriting EXIF by hand
    decide the image is encoded again, with its orientation applied
      because dropping EXIF alone would also drop the orientation and show many phone photos on their side; at quality 95 the difference cannot be seen
    decide offered only when a position is found
      because without one there is nothing to protect, and an action on every image would be noise
    decide a copied file is pasted as a new file with its own name, in Shapeshifter's temporary folder
      because the original must stay as it is, and the name is what the receiver sees
    decide MetadataExtractor to find the position
      because it reads EXIF and XMP in every common image type, is Apache-2.0 licensed and has no native parts
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, TempFolder, Localization
  sources
    the owner's request in this change: "I6 Paste without location"
```
