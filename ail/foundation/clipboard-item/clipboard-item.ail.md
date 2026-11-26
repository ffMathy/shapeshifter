# Clipboard item

```ail
node ClipboardItem : data   level: system   @existing
  why   Every action takes the item it acts on.   <- Foundation
  what
    type ClipboardContentKind = "Text" | "Image" | "File" | "FileCollection" | "Other"
    type ClipboardSnapshot = { Text: text or nothing, Image: bytes or nothing, Files: list of text, Kind: ClipboardContentKind }
      a snapshot is made with only the parts it holds: ClipboardSnapshot { Text: "a link" }; ClipboardSnapshot { Text: "chart", Image: some image bytes }
      Kind is worked out from what the snapshot holds and is never given: Image when it holds an image and no text, Text when it holds text, File or FileCollection for one or several files, else Other
    type ClipboardItem = { Id, Snapshot: ClipboardSnapshot, Kind: ClipboardContentKind, CapturedAt: time }
      Id is the item's unique id, usable as a key; Kind is Snapshot's Kind; CapturedAt is when it was copied, with its offset from UTC
  sources
    src/Shapeshifter.Core/Clipboard/ClipboardItem.cs, ClipboardSnapshot.cs
```
