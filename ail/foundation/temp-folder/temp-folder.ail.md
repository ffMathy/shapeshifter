# Temp Folder

```ail
node TempFolder : data   level: system   @existing
  why   Actions that make files put them where Shapeshifter cleans up.   <- Foundation
  what
    ActionContext.Paths.TempDirectory: text   — Shapeshifter's temporary folder; it exists, and it is emptied every time Shapeshifter starts
      on Windows and macOS %TEMP%/Shapeshifter, on Linux $XDG_RUNTIME_DIR/shapeshifter or <data>/temp
      in checks, ActionTestHarness gives each harness a fresh folder of its own (harness.Paths.TempDirectory), deleted when the harness is disposed
  sources
    src/Shapeshifter.Core/Settings/AppPaths.cs, TempDirectory; src/Shapeshifter.Desktop/App.axaml.cs, CleanTemporaryFiles
```
