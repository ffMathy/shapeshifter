# Show copied files in the file manager

"Show in file manager" on copied files opens the folder they are in, with the
first of them selected.

```ail
node ShowInFileManager : fn   level: system
  why   Files copied a while ago, or in another app, are often wanted again
        where they live: to rename them, open a neighbour, or see what else
        is there. Finding the folder by hand means retracing where they came
        from.   <- Actions
  what
    ShowInFileManagerAction is an IClipboardAction with Id "show-in-file-manager" and Order 232:
      CanExecute(item) is true when item.Kind is File or FileCollection, and the first path in item.Snapshot.Files is not a network path (starting \\ or //) and names an existing file or folder
      GetTitle(item) is Strings.ActionShowInFileManager, "Show in file manager"
      GetDescription(item) is Strings.ActionShowInFileManagerDescription, "Opens the folder these files are in, with them selected." for several files, or Strings.ActionShowFileInFileManagerDescription, "Opens the folder this file is in, with it selected." for one
      ExecuteAsync calls context.Launcher.RevealInFileManager(the first path in item.Snapshot.Files); nothing is pasted, the clipboard and the history are unchanged
    examples
      CanExecute(files [an existing file]) => true
      CanExecute(files [an existing folder]) => true
      CanExecute(files [the path of nothing]) => false; it was moved or deleted since it was copied
      CanExecute(files ["\\\\server\\share\\a.txt"]) => false
      CanExecute(text "hello") => false
      GetDescription(files [an existing file, another]) => "Opens the folder these files are in, with them selected."
      ExecuteAsync(files ["/home/anna/a.txt", "/home/anna/b.txt"], both existing) => the launcher reveals "/home/anna/a.txt"; nothing is pasted
  how
    decide the first file is shown
      because copied files almost always come from one folder, and file managers select one item reliably on every system
    decide only files that still exist, never network paths
      because a file moved since it was copied cannot be shown, and asking a network share can take seconds while the actions are worked out
  uses      ClipboardAction, ClipboardItem, FileManager, Localization
  sources
    the owner's request in this change: "F7 Show in file manager"
```
