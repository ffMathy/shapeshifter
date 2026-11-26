# Paste trimmed

"Paste trimmed" pastes a text without the spaces, tabs and blank lines at its
start and end. It is offered only when there is something to take away.

```ail
node PasteTrimmed : fn   level: system
  why   Text copied from a web page, a terminal or a document often brings a
        trailing line break or leading spaces along, which break a form field,
        a password box or a command line; removing them by hand means pasting
        first and then deleting.   <- Actions
  what
    PasteTrimmedAction is an IClipboardAction with Id "paste-trimmed" and Order 24:
      CanExecute(item) is true when item.Kind is Text, item.Snapshot.Text holds something other than white space, and it starts or ends with white space
      white space is what Unicode calls white space: spaces, tabs, line breaks, non-breaking spaces and the like
      GetTitle(item) is Strings.ActionPasteTrimmed, "Paste trimmed"
      GetDescription(item) is Strings.ActionPasteTrimmedDescription, "Pastes this text without the spaces and blank lines at its start and end."
    ExecuteAsync pastes a new snapshot holding only the trimmed text, ClipboardSnapshot { Text: the trimmed text }, with context.WriteAndPasteAsync; nothing is added to the history
    examples
      CanExecute(text "  hello\n") => true
      CanExecute(text "hello") => false
      CanExecute(text "a b") => false; white space inside the text does not count
      CanExecute(text "   ") => false
      CanExecute(text " hello") => true; a non-breaking space is white space
      CanExecute(PNG image) => false
      ExecuteAsync(text "\n\n  two\n  lines \t\r\n") => pastes { Text: "two\n  lines" }
      ExecuteAsync(text " x") => the history is unchanged
  how
    decide only the start and the end are trimmed; spaces, indentation and blank lines inside stay
      because the text's own layout is the user's; only what came along by accident goes
    decide offered only when trimming changes the text
      because an action that would paste the same as Paste is noise in the list (OfferedActions)
    decide formatted text pastes its plain text, trimmed
      because trimming inside a web page's markup would leave its formatting unpredictable; Paste as plain text does the same
    decide what is pasted is not added to the history
      because Paste as plain text and Paste as file do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "T4 Paste trimmed"
```
