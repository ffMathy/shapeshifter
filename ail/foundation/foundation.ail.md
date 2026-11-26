# Foundation

Hand-written code that generated code builds on. These nodes are `@existing`:
`ail build` never compiles them, and the nodes that `use` them see their
contracts. Each states only what generated code needs; the code itself, named
under `sources`, holds the rest.

```ail
node Foundation : library   level: system
  why   Generated code needs the clipboard's types, the actions' interface and
        the translated text, which stay hand-written while the app moves to
        generated code one node at a time.   <- Shapeshifter
  children  ClipboardItem, ImageFormat, ClipboardAction, ActionContext, Localization
```

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

```ail
node ImageFormat : fn   level: system   @existing
  why   An uploaded or saved image keeps its own type.   <- Foundation
  what
    ImageFormat.GetExtension(image: bytes) -> text   — "png", "jpg", "gif", "bmp", "webp" or "tiff"; "png" when unknown
    ImageFormat.GetMediaType(image: bytes) -> text   — "image/png", "image/jpeg" and so on; "image/png" when unknown
  sources
    src/Shapeshifter.Core/Clipboard/ImageFormat.cs
```

```ail
node ClipboardAction : data   level: system   @existing
  why   The picker lists actions by one interface, whoever wrote them.   <- Foundation
  what
    type IClipboardAction = { Id: text, Order: integer, GetTitle(item: ClipboardItem) -> text, GetDescription(item: ClipboardItem) -> text, CanExecute(item: ClipboardItem) -> boolean, ExecuteAsync(item: ClipboardItem, context: ActionContext, cancellation) -> later nothing }
      ExecuteAsync's cancellation is optional, and defaults to none
  uses  ClipboardItem, ActionContext
  sources
    src/Shapeshifter.Core/Actions/IClipboardAction.cs
```

```ail
node ActionContext : data   level: system   @existing
  why   An action reaches the clipboard, the history and the web only through
        what it is handed.   <- Foundation
  what
    type ActionContext = { Http, History, Logger }
      Http is the app's shared web client, which already sends User-Agent "Shapeshifter/<version>"
      History.Add(snapshot: ClipboardSnapshot) -> ClipboardItem   — adds it as the newest item
      Logger.Information(template: text, values) -> nothing   — a structured log message, such as "Uploaded an image to {Service}"
    ActionContext.WriteAndPasteAsync(snapshot: ClipboardSnapshot, cancellation) -> later nothing   — writes the clipboard, waits 40 ms, then simulates Ctrl+V
  uses  ClipboardItem
  sources
    src/Shapeshifter.Core/Actions/IClipboardAction.cs, ActionContext
```

```ail
node Localization : data   level: system   @existing
  why   Every user-visible text is translated.   <- Foundation
  what
    Strings.<Key>: text   — the text of that key in Strings.resx, in the user's language
    Localizer.Format(format: text, values) -> text   — fills in {0}, {1} … in the user's culture
  sources
    src/Shapeshifter.Core/Localization/Strings.resx, Localizer.cs
```
