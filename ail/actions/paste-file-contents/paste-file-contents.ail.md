# Paste file contents

"Paste file contents" pastes the text inside one copied text file, instead of
the file.

```ail
node PasteFileContents : fn   level: system
  why   A log, a configuration file or a snippet copied in a file manager
        often needs to go into a chat, a ticket or another file as text;
        getting it meant opening the file in an editor, selecting everything
        and copying again.   <- Actions
  what
    PasteFileContentsAction is an IClipboardAction with Id "paste-file-contents" and Order 72:
      CanExecute(item) is true when item.Kind is File and PasteFileContentsAction.ReadText(the one path in item.Snapshot.Files) gives a text
      GetTitle(item) is Strings.ActionPasteFileContents, "Paste file contents"
      GetDescription(item) is Strings.ActionPasteFileContentsDescription, "Pastes the text inside this file instead of the file."
      ExecuteAsync reads the file again with ReadText and pastes ClipboardSnapshot { Text: that text } with context.WriteAndPasteAsync; nothing is added to the history; when ReadText now gives nothing (the file changed or went away), it throws InvalidOperationException and pastes nothing
    PasteFileContentsAction.MaxBytes: integer = 1048576
    PasteFileContentsAction.ReadText(path: text) -> text or nothing
      gives nothing for a network path (starting \\ or //), for a path that is not an existing file (a folder included), and for a file larger than MaxBytes
      reads the bytes: a UTF-8 byte order mark is dropped and the rest read as UTF-8; a UTF-16 byte order mark (FF FE or FE FF) means UTF-16 in that byte order; without one, the bytes must be valid UTF-8
      gives nothing when the bytes are not valid in that encoding, or when the text holds a character below U+0020 other than tab, line feed, carriage return and form feed (as a NUL byte in a binary file would)
      an empty file gives nothing
      else gives the text, unchanged: its line endings and its final line break, if any, stay as they are
    examples
      ReadText(a file holding the UTF-8 bytes of "line one\nline two\n") => "line one\nline two\n"
      ReadText(a file holding EF BB BF and then "héllo") => "héllo"
      ReadText(a file holding FF FE and then "hi" in UTF-16 little endian) => "hi"
      ReadText(a file holding the bytes 89 50 4E 47 00 01, a PNG's start) => nothing
      ReadText(a file holding C3 28) => nothing; not valid UTF-8
      ReadText(an empty file) => nothing
      ReadText(a file of 1048577 bytes of "a") => nothing; larger than MaxBytes
      ReadText(a folder's path) => nothing
      ReadText(the path of nothing) => nothing
      CanExecute(files [a text file]) => true
      CanExecute(files [a text file, another text file]) => false; only one file
      CanExecute(text "hello") => false
      ExecuteAsync(files [a file holding "a\r\nb"]) => pastes { Text: "a\r\nb" }; the history is unchanged
  how
    decide one file, of text, at most 1 MB
      because several files would need a rule for joining them, binary files paste as noise, and which actions an item offers is worked out when it is copied, where reading a large file would be slow
    decide UTF-8, or UTF-16 with a byte order mark; nothing else is guessed
      because UTF-8 is what almost every text file is today, and Windows tools that write UTF-16 mark it; guessing an old code page would paste wrong letters
    decide the text is pasted exactly as it is in the file
      because the user asked for the file's contents, line endings and all
    decide the file is read again when the action runs
      because it may have changed since it was copied, and the user wants what it holds now
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "F3 Paste file contents"
```
