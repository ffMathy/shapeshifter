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
  children  ClipboardItem, ImageFormat, ClipboardAction, ActionContext, Localization, CopiedHtml, Launcher, NewContent
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

```ail
node CopiedHtml : data   level: system   @existing
  why   Formatted text keeps its formatting as HTML, which some actions read.   <- Foundation
  what
    item.Snapshot.RawFormats: list of RawClipboardFormat — every format the source app offered, as copied, in its order
    type RawClipboardFormat = { Name: text, Data: bytes }
    RawClipboardFormat(name: text, data: bytes) -> RawClipboardFormat   — makes one, by position
      a snapshot with one: ClipboardSnapshot { Text: "Hello World", RawFormats: [RawClipboardFormat("text/html", the UTF-8 bytes of "<b>Hello</b> World")] }
    ClipboardFormatClassifier.IsHtml(name: text) -> boolean   — in Shapeshifter.Core.Clipboard; true for every platform's HTML format name: "text/html", "HTML Format" and the like
    Shapeshifter.Core.Text.HtmlText.Decode(data: bytes) -> text   — it lives in Shapeshifter.Core.Text, which generated code must import with a using; the HTML in a format's Data: Windows' CF_HTML header is removed, UTF-16 is recognised by its byte order mark, else UTF-8
  sources
    src/Shapeshifter.Core/Clipboard/ClipboardSnapshot.cs, RawClipboardFormat; ClipboardFormatClassifier.cs, IsHtml; src/Shapeshifter.Core/Text/HtmlText.cs, Decode
```

```ail
node Launcher : data   level: system   @existing
  why   Some actions open something outside Shapeshifter: a link in the
        browser, a new e-mail.   <- Foundation
  what
    ActionContext.Launcher.OpenUrl(link: an absolute link) -> nothing
      opens an http, https or mailto link with the system's default app for it, at once; it returns before that app has started, and only logs when the system cannot open it
      any other kind of link throws ArgumentException
      in checks, ActionTestHarness gives the context a recording launcher: harness.Launcher.OpenedUrls lists, in order, the links opened (a list of absolute links), and nothing is opened for real
  sources
    src/Shapeshifter.Core/Platform/ISystemServices.cs, ILauncher; src/Shapeshifter.Platform/Common/Launcher.cs
```

```ail
node NewContent : data   level: system   @existing
  why   Actions that make something new put it on the clipboard and in the
        history, so a plain paste afterwards inserts it.   <- Foundation
  what
    ActionContext.CopyNewContentAsync(snapshot: ClipboardSnapshot, cancellation) -> later nothing
      writes the snapshot to the clipboard, then adds it to the history as its newest item; nothing is pasted
      in checks, ActionTestHarness shows it as the last of harness.Backend.Writes and the first of harness.History.Items, and harness.Paste.Calls stays 0
  sources
    src/Shapeshifter.Core/Actions/IClipboardAction.cs, ActionContext.CopyNewContentAsync
```
