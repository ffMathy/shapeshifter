# New Content

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
