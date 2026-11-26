# Copied file paths

A copied text that is the path of a file or folder on the computer, such as
C:\Users\anna\report.pdf or ~/Pictures, can be turned into the file itself:
"Copy file at this path".

```ail
node FilePath : fn   level: system
  why   Paths are copied from terminals, logs, chats and documents, and what
        the user wants is usually the file, to attach it or move it. Getting
        it meant pasting the path into a file manager's address bar first.   <- Actions
  what
    FilePath.Resolve(text: text or nothing) -> text or nothing
      takes the text without the white space at its start and end, and without one pair of " or ' around it
      a file:// link is turned into its local path, with %-codes decoded
      ~ alone, or ~/ or ~\ at the start, stands for the user's home folder
      gives nothing when the text holds a line break, is longer than 1024 characters, or is not an absolute path once ~ is replaced: on Windows a drive (C:\ or C:/), on macOS and Linux a leading /
      gives nothing for network paths: \\server\share, //server/share, and file:// links that name a host
      gives nothing unless a file or a folder exists there; else gives the full path, as the system writes it
    CopyFileAtPathAction is an IClipboardAction with Id "copy-file-at-path" and Order 110:
      CanExecute(item) is true when item.Kind is Text and Resolve(item.Snapshot.Text) gives a path
      GetTitle(item) is Strings.ActionCopyFileAtPath, "Copy file at this path"
      GetDescription(item) is Strings.ActionCopyFileAtPathDescription, "Copies the file or folder itself, ready to paste in a file manager or an email."
      ExecuteAsync puts ClipboardSnapshot { Files: [the path] } on the clipboard with context.CopyNewContentAsync, which also adds it to the history; nothing is pasted
    examples
      Resolve(the path of an existing file, such as "/home/anna/report.pdf") => "/home/anna/report.pdf"
      Resolve("\"/home/anna/My report.pdf\"") => "/home/anna/My report.pdf", when it exists
      Resolve("file:///home/anna/My%20report.pdf") => "/home/anna/My report.pdf", when it exists
      Resolve("~/report.pdf") => the home folder's report.pdf, when it exists
      Resolve("~") => the home folder
      Resolve(the path of an existing folder) => that path
      Resolve(the path of nothing that exists) => nothing
      Resolve("report.pdf") => nothing; not absolute
      Resolve("\\\\server\\share\\a.txt") => nothing; a network path
      Resolve("file://server/share/a.txt") => nothing
      Resolve("/home/anna/a.txt and /home/anna/b.txt") => nothing, unless a file has that whole name
      Resolve(two paths on two lines) => nothing
      CanExecute(text: an existing file's path) => true
      CanExecute(PNG image) => false
      ExecuteAsync(text "/home/anna/report.pdf", which exists) => the clipboard holds { Files: ["/home/anna/report.pdf"] }, which is the history's newest item; nothing is pasted
  how
    decide only absolute local paths of things that exist
      because a relative path depends on a folder Shapeshifter cannot know, and an action that fails on a path that is not there is noise
    decide network paths are left out
      because asking whether a file exists on a network share can take seconds, and which actions an item offers is worked out when it is copied, where nothing slow may happen
    decide copying the file adds it to the history, like Copy image from link
      because the file is new content the user may paste more than once, into a file manager or an e-mail
  uses      ClipboardAction, ClipboardItem, NewContent, Localization
  sources
    the owner's request in this change: "R9 Copy the file at this path"
```
