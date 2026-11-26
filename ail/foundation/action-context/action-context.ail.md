# Action context

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
