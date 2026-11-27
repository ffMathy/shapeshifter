# Shapeshifter

A clipboard manager for Windows, macOS and Linux: copy many things, paste any
of them. This spec is being written in reverse from the existing app
(`/ail:adopt`), one area at a time. Every area of the app is now covered.

```
shapeshifter.ail.md                                          Shapeshifter (this file)
paste-gesture/paste-gesture.ail.md                           PasteGesture: a quick paste, or hold for the history
paste-gesture/pick-from-history/pick-from-history.ail.md     PickFromHistory: choose an older item, or cancel
paste-gesture/keep-history-open/keep-history-open.ail.md     KeepHistoryOpen: Space, then Enter or Esc
paste-gesture/gesture-rules/gesture-rules.ail.md             GestureRules: the keys, as a pure state machine
capture/                                                     Capture: what joins the history, the copy notification, tips
picker/                                                      Picker: the window, rows, preview, footer, looks, languages
actions/                                                     Actions: paste variants, delete, pin, remember the history
search/                                                      SearchHistory: S to search the history
app/                                                         App: tray, settings, single instance, install, updates
foundation/                                                  Foundation: hand-written code that generated code builds on
```

## Target

The app as it is built today. Changing this block, then `/ail:build`, moves it
to another technology; the end-to-end checks in `checks/e2e/` prove the new
app behaves like this one.

`ail build` compiles the logic: `fn` and `data` nodes, into
`src/Shapeshifter.Core/Ail/`. Flows, views and the app itself are hand-written
and proven by their end-to-end checks, which `ail build` runs against the app
(`run.app`). Hand-written code that generated code depends on is described by
`@existing` nodes in `ail/foundation/`. A node moves to generated code once it
is built: its hand-written counterpart is deleted in the same change.

```ail
target
  code         C# 14 on .NET 11, nullable enabled, warnings as errors; Avalonia 12 for windows; SharpHook for the global keyboard hook and simulated keys
  structure    Shapeshifter.Core holds every rule as plain, testable code; Shapeshifter.Platform touches the operating system; Shapeshifter.Desktop is the Avalonia app
  naming       PascalCase types and members, file named after the node; hand-written code has one public type per file, while a generated file holds every public type of its node
  namespaces   generated code is in the file-scoped namespace Shapeshifter.Core.Ail and starts with: using Shapeshifter.Core.Actions; using Shapeshifter.Core.Clipboard; using Shapeshifter.Core.Localization; (the @existing nodes live there, by the names their contracts give)
  comments     every public type and member has an XML doc comment saying what it is for
  arguments    a public member that takes a reference-type argument throws ArgumentNullException (ArgumentNullException.ThrowIfNull) when it is missing, before anything else; the derived checks don't test this, the hand-written ActionCatalogTests do
  copy         user-visible text comes from Strings.resx and its translations, never from code: Strings.<Key> for each key the spec names
  path.fn      src/Shapeshifter.Core/Ail/{dir}/{name}.cs
  path.data    src/Shapeshifter.Core/Ail/{dir}/{name}.cs
  generated    src/Shapeshifter.Core/Ail/**
  tests        xUnit v3 on Microsoft.Testing.Platform, in tests/Shapeshifter.Core.Tests, built like all code with warnings as errors and the .NET analyzers (so a list's Count and indexer, never Enumerable.Count() or First() on a list; ConfigureAwait(false) is not needed in tests); a check is a public sealed class in the file-scoped namespace Shapeshifter.Core.Ail.Tests, named after its file; actions are checked with ActionTestHarness (below); end-to-end checks are bun tests that drive the desktop with xdotool
  test harness ActionTestHarness, internal to the test project in namespace Shapeshifter.Core.Tests.Actions: `using var harness = new ActionTestHarness();` gives harness.Context, an ActionContext wired to fakes; harness.Handler.Respond is a function from each HTTP request to its answer, and harness.Handler.RequestedUris lists the requests made; harness.AddToHistory(snapshot) returns the ClipboardItem it added, whose CapturedAt a check may set; harness.Backend.Writes lists the snapshots written to the clipboard, harness.Paste.Calls counts simulated pastes, harness.History.Items lists the history newest first; ActionTestHarness.Ct is the cancellation token to pass
  checks.path  tests/Shapeshifter.Core.Tests/Ail/{dir}/{name}Tests.cs
  checks.run   dotnet test --project tests/Shapeshifter.Core.Tests --filter-class "Shapeshifter.Core.Ail.Tests.{name}Tests"
  checks.path.view  tests/Shapeshifter.Desktop.Tests/Ail/{dir}/{name}Tests.cs
  checks.run.view   dotnet test --project tests/Shapeshifter.Desktop.Tests --filter-class "Shapeshifter.Desktop.Tests.Ail.{name}Tests"
  checks.path.flow  tests/Shapeshifter.Desktop.Tests/Ail/{dir}/{name}Tests.cs
  checks.run.flow   dotnet test --project tests/Shapeshifter.Desktop.Tests --filter-class "Shapeshifter.Desktop.Tests.Ail.{name}Tests"
  checks.harness  views and flows are written by hand (Shapeshifter.Desktop, Avalonia), so their checks drive the real app on
               Avalonia's headless platform, in tests/Shapeshifter.Desktop.Tests: a check is a public sealed class in the
               file-scoped namespace Shapeshifter.Desktop.Tests.Ail, named after its file, each test
               `public Task Name() => AppHarness.RunAsync(async app => { ... }, new AppOptions { ... });` (await
               everything inside; the options are optional). AppOptions: Look (Look.Linux, the default; Look.Windows;
               Look.MacOS), Dark (the Windows look in dark), Language (a culture name such as "de"), Modifier ("Ctrl", the
               default, or "Cmd"), StoreCopy (installed from an app store), ScreenHeight (the screen's height in pixels, 1080 by default), NoCompositor (a desktop without a compositor, such as a
               bare X server: no window can be see-through; by default there is one). The checks run on Windows, macOS and Linux: a scenario
               that names one system ("on Linux") applies there only, so its test starts with Assert.SkipUnless(OperatingSystem.IsLinux(),
               "This scenario applies on Linux only.") before it runs the app. AppHarness, the real engine, history, actions,
               picker, copy notification and settings window with fakes for the system: await app.CopyAsync(text, window)
               (window: the name of the window it was copied in, which gets an icon, or null where none can be named),
               CopyImageAsync(width, height), CopyFilesAsync(full paths), CopyFormattedAsync(text, html), PinAsync(text,
               ago); HoldCtrlVAsync() (holds Ctrl, or Cmd, taps V and keeps Ctrl down until the picker appears),
               TapAsync(GestureKey.X), DownAsync(key), UpAsync(key), ReleaseCtrlAsync() (lets go of Ctrl and waits until
               the picker is gone), TypeSearchAsync(query) (taps S first when the search has not begun); OpenSettings() returns the
               settings window. GestureKey: Modifier, Trigger (V), Up, Down, Left, Right, Delete, Escape, Space, Enter,
               Search (S), Digit1 to Digit9. What there is to see: app.Picker (the picker's window), app.PickerVisible,
               app.Notification (the copy notification's window), app.SettingsWindow; AppHarness.Texts(window) lists
               every text it shows, in reading order; AppHarness.Find<T>(window) its visible controls of a type.
               app.HistoryRows and app.ActionRows are the rows of the history and of the actions that the list has made
               (each a ListBoxItem, top first; IsSelected on the chosen one; a long list only makes the rows near what
               shows, so count the history's items with app.History.Items), app.ChosenRow the chosen history row, AppHarness.IsFullyShown(control)
               whether all of it shows (inside its window and the scrolled part of its list); await app.ShowTipAsync(title,
               message) shows a coaching tip in the copy notification. What a scenario says about how something looks
               (colours, rounding, sizes, where on the screen) is judged from its screenshot against the look: the check
               asserts the texts, states and order it names, and saves the screen.
               Pictures and previews load in the background: wait for them with await AppHarness.WaitUntilAsync(() =>
               condition, "what"). What happened: app.Paste.Pasted (what each paste put into the application in front, a
               file as much as a text: "nothing is typed" means no paste holds a text), app.Clipboard.Current
               and .Written, app.Launcher.OpenedUrls and .OpenedFolders, app.History.Items, app.Paths. Each test ends with
               AppHarness.SaveScreen(window, "<the file name it was given>") on the window its scenario ends on.
```

## Glossary

```ail
glossary
  term history      plain "everything copied while Shapeshifter runs, newest first, pinned items on top"   formal ClipboardHistory
  term item         plain "one thing that was copied"                                                        formal ClipboardItem
  term picker       plain "the window that shows the history while you choose"                              formal PickerWindow
  term "the gesture" plain "holding Ctrl and V to paste, or to choose from the history"                     formal PasteGestureStateMachine
  term "quick paste" plain "Ctrl+V let go before the history appears: an ordinary paste"                    formal PasteCurrentClipboard
  term "show delay" plain "how long V must stay held before the history appears"                            formal ShapeshifterSettings.PasteDelayMilliseconds, 300 ms by default
  term "kept open"  plain "the history stays up without holding Ctrl, after Space"                          formal PickerSelection.IsHeldOpen
  term Ctrl         plain "Ctrl, or Cmd on macOS"                                                            formal KeyMap gesture modifier
  term action       plain "something to do with an item other than pasting it as it is"                     formal IClipboardAction
  term "the actions" plain "the list to the right of the history, for the chosen item"                     formal PickerPane.Actions
  term "pinned item" plain "an item kept at the top of the history and saved on the computer"               formal ClipboardItem.IsPinned, PinnedItemStore
  term "formatted text" plain "text that carries formatting, such as bold from a web page"                  formal a Text item with an HTML or RTF format
  term "other data" plain "something copied that is neither text, an image nor files"                      formal ClipboardContentKind.Other
  term search       plain "typing words in the picker to find an item"                                       formal PickerSearch
  term result       plain "an item the search found"                                                         formal PickerSelection.ItemIds while searching
  term look         plain "the visual style matched to the system: Mica Sheets, Refraction or Boxed Sheet"   formal PlatformLook.Look
  term accent       plain "the system's highlight colour, in shades that stay readable"                     formal AccentPalette
  term "preview card" plain "the larger view of the chosen item, above its actions"                         formal PickerWindow preview pane
  term tile         plain "the square at the start of a row: a thumbnail, file icons, a colour or a line icon" formal Border.tile
  term "display language" plain "the language Shapeshifter shows: the system's, or SHAPESHIFTER_LANGUAGE"    formal Localizer, CurrentUICulture
  term "tray icon"  plain "Shapeshifter's icon in the system tray or menu bar, with its menu"               formal App.CreateTrayIcon
  term "settings window" plain "the one window with every setting and the version"                          formal SettingsWindow
  term "install location" plain "where the first start copies Shapeshifter for the user"                    formal InstallLayout.Target
  term "start at login" plain "starting Shapeshifter when the user signs in"                                formal IAutostartService
  term "release build" plain "a downloaded single-file build, the only kind that installs and updates itself" formal RunningApp.IsSingleFile
  term "pull request build" plain "a test build of a pull request, which never installs or updates"         formal ReleaseVersion.IsPullRequestBuild
  term notification plain "the small 'Copied · N items in your history' message after a new copy"          formal ICopyNotifier, ToastWindow
  term tip          plain "a hint, in the notification's place, that the history could have saved a copy"  formal CoachingTip
  term secret       plain "content a password manager marks as not to be recorded"                          formal ClipboardFormatClassifier.IsExcludedFromHistoryAsync
  term source       plain "the app and window in front when something was copied"                           formal ClipboardSource
  term "newer copy" plain "a running copy of a higher version, which ends an older one"                     formal RunningInstance.ShouldReplace
```

## Design tokens

The Linux look (Boxed Sheet), from `src/Shapeshifter.Desktop/Styles/Linux.axaml`. The other looks' values are in
their own nodes (MicaSheets, Refraction).

```ail
catalog
  tokens
    color.text=#2E2E33  color.text.dim=#5E5C64  color.window=#FAFAFB  color.card=#FFFFFF  color.tile=#F6F5F4
    color.glyph=#3D3846  color.hairline=#12000006  color.border=#1A000006  color.opaque.border=#C0BFBC
    color.warning.fill=#33E5A50A  color.warning.border=#59C88800
    accent.default=#3584E4  accent.text=#1C71D8  accent.dark=#255C9F
    radius.sheet=12px  radius.card=12px  radius.tile=8px  radius.key=6px  radius.ring=8px  radius.command=17px
    size.row=64px  size.tile=40px  size.header=46px  size.history=416px  size.actions=284px  size.preview=196px  size.settings=540px
    font.title=14px  font.source=12.5px  font.time=12px  font.heading=15px  font.key=11.5px
```

---

```ail
node Shapeshifter : system   level: product
  why   Copying one thing pushes out the last; people lose what they copied a
        moment ago and go back to copy it again.
  what
    1. A quick Ctrl+V pastes exactly as it would without Shapeshifter.         refined-by: PasteGesture
    2. Holding Ctrl+V shows everything copied, to paste any of it.             refined-by: PasteGesture
    3. You choose with the keys you are already holding, and can cancel.       refined-by: PickFromHistory
    4. The history can stay open to look through it without holding a key.    refined-by: KeepHistoryOpen
    5. Everything copied in any app joins the history, newest first, never twice, secrets left out.   refined-by: Capture
    6. Each copy is acknowledged with a short notification, and tips show how the history could have saved a copy.   refined-by: CopyNotification, CoachingTips
    7. The history shows what each item is, where it came from and when, and previews the chosen one.   refined-by: Picker
    8. The picker appears near the top of the screen and never takes the keyboard from the app pasted into.   refined-by: OpenPicker
    9. Shapeshifter looks like part of each system, in its accent colour, and speaks 21 languages besides English.   refined-by: Looks, Languages
    10. An item can be pasted in another shape: without its formatting, or as a file.   refined-by: PasteActions
    11. Items can be pinned to stay on top, and deleted; pinned items survive a restart.   refined-by: PinItems, DeleteItems
    12. Only pinned items are written to the computer, unless the user asks to remember the whole history.   refined-by: RememberHistory
    13. Typing a few words finds anything copied earlier, and Enter pastes it into the app the user came from.   refined-by: SearchHistory
    14. Shapeshifter waits in the system tray; every setting takes effect at once.   refined-by: TrayMenu, ChangeSettings
    15. It installs itself for the user without administrator rights, starts at login and keeps itself up to date.   refined-by: SelfInstall, AutoUpdate
  entry     PasteGesture
  children  PasteGesture, Capture, Picker, Actions, SearchHistory, App, Foundation
  sources
    README.md, sections "Hold Ctrl+V" and "Good to know"
    docs/architecture.md
```
