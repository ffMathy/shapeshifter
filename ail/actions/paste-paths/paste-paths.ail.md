# Paste paths

"Paste path" or "Paste paths" pastes where copied files are, as text: the
full path of each, one per line.

```ail
node PastePaths : fn   level: system
  why   A file's location is needed as text all the time: in a terminal, a
        configuration file, a bug report or a chat. File managers copy the
        file itself, and getting its path means a detour through a properties
        dialog or a context menu.   <- Actions
  what
    PastePathsAction is an IClipboardAction with Id "paste-paths" and Order 70:
      CanExecute(item) is true when item.Kind is File or FileCollection
      GetTitle(item) is Strings.ActionPastePath, "Paste path", for one file, else Strings.ActionPastePaths, "Paste paths"
      GetDescription(item) is Strings.ActionPastePathDescription, "Pastes where this file is as text.", for one file, else Strings.ActionPastePathsDescription, "Pastes where these files are as text, one per line."
      ExecuteAsync pastes ClipboardSnapshot { Text: PastePathsAction.Join(item.Snapshot.Files) } with context.WriteAndPasteAsync; nothing is added to the history
    PastePathsAction.Join(paths: list of text) -> text
      the paths as they were copied, in their order, joined by the system's line ending: CR LF on Windows, LF elsewhere; no line ending after the last
    examples
      Join(["/home/anna/report.pdf"]) => "/home/anna/report.pdf"
      Join(["/a/x.txt", "/a/y.txt"]) on Linux => "/a/x.txt\n/a/y.txt"
      Join(["C:\\a\\x.txt", "C:\\a\\y.txt"]) on Windows => "C:\\a\\x.txt\r\nC:\\a\\y.txt"
      Join(["/a/My file.txt"]) => "/a/My file.txt"; no quotes are added
      GetTitle(files ["/a/x.txt"]) => "Paste path";  GetTitle(files ["/a/x.txt", "/a/y.txt"]) => "Paste paths"
      CanExecute(files ["/a/x.txt"]) => true
      CanExecute(text "/a/x.txt") => false
      CanExecute(PNG image) => false
      ExecuteAsync(files ["/a/x.txt", "/a/y.txt"]) => pastes { Text: Join(["/a/x.txt", "/a/y.txt"]) }; the history is unchanged
  how
    decide the full paths as copied, one per line, with no quotes
      because one path per line is what terminals, scripts and lists take; quoting depends on where it is pasted, and the user can add it there
    decide the system's line ending
      because Windows apps such as Notepad expect CR LF, and every other system LF
    decide the title says path or paths
      because the user sees how many will be pasted
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "F1 Paste paths"
```
