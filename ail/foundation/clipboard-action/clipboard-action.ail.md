# Clipboard action

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
