# Copied html

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
