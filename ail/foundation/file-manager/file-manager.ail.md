# File Manager

```ail
node FileManager : data   level: system   @existing
  why   Some actions show a file where it lives.   <- Foundation
  what
    ActionContext.Launcher.RevealInFileManager(path: text) -> nothing
      opens the folder that holds the file or folder at path in the system's file manager, with it selected: Explorer on Windows, Finder on macOS, and on Linux the file manager that answers the freedesktop FileManager1 interface, else the folder itself; it only logs when that fails
      in checks, ActionTestHarness gives the context a recording launcher: harness.Launcher.RevealedPaths lists, in order, the paths revealed, and nothing is opened for real
  sources
    src/Shapeshifter.Core/Platform/ISystemServices.cs, ILauncher.RevealInFileManager; src/Shapeshifter.Platform/Common/Launcher.cs
```
