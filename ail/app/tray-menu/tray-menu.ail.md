# The tray icon and its menu

The one thing of Shapeshifter that is always on screen: an icon in the system
tray, with a menu.

The system draws the icon and the menu, not Shapeshifter, so they have no
screen of their own to picture: what they show is under `what`, and the
end-to-end check proves it on the desktop.

```ail
node TrayMenu : flow   level: experience
  why   With no main window, people need a visible sign that Shapeshifter runs, a way
        into its settings, and a quick way to report a problem.   <- App
  what
    the menu is the platform's own, in this order: Settings…, Open log folder, Clear history, Check for
    updates…, a separator, Report a problem…, About Shapeshifter, a separator, Quit Shapeshifter
    (a copy from an app store has no "Check for updates…")
    the icon is a clipboard; on Linux a symbolic glyph in the panel's tone, on a Mac a template image in the menu bar
    Scenario: clicking the tray icon opens the settings
      Given Shapeshifter runs and its icon is in the tray
      When the user clicks the icon
      Then the settings window opens
    Scenario: Settings… opens the settings, and only once
      When the user chooses "Settings…" in the tray menu
      Then the settings window opens
      When the user chooses "Settings…" again
      Then the same window comes forward and no second one opens
    Scenario: About Shapeshifter opens the settings
      When the user chooses "About Shapeshifter" and the version in the tray menu
      Then the settings window opens, showing the name and the version
    Scenario: Report a problem… opens a new GitHub issue with the version filled in
      When the user chooses "Report a problem…"
      Then the browser opens a new issue on the project's GitHub page, labelled as a bug
      And its text asks what happened, what was expected and how to reproduce it
      And it names the version, the system, the processor and, on Linux, X11 or Wayland
      And the settings window does not open
    Scenario: Open log folder opens the folder of the logs
      When the user chooses "Open log folder"
      Then the file manager opens the folder that holds the logs
    Scenario: Check for updates… opens the releases page when this copy cannot update itself
      Given this copy cannot update itself, as a build from source cannot
      When the user chooses "Check for updates…"
      Then the browser opens the project's releases page
      And the settings window does not open
    Scenario: a copy from an app store offers no update check
      Given Shapeshifter was installed from the Microsoft Store
      When the user opens the tray menu
      Then there is no "Check for updates…" item
      And the other items are as in any copy
    Scenario: Clear history forgets what was copied before
      Given "older" and then "latest" were copied
      When the user chooses "Clear history"
      And holds Ctrl and V until the history appears, presses V again and lets go of Ctrl
      Then "latest", still on the clipboard, is pasted, not "older"
    "Quit Shapeshifter" ends it; the gesture and the icon go away until it is started again.
    Pointing at the icon shows "Shapeshifter" and the version.
    A copy that can update itself checks right away on "Check for updates…" and shows the settings window,
      whose "Automatic updates" row says how the check goes.
  how
    decide the icon, not a window, is the way in; a click on it opens the settings
      because a menu-bar or tray utility has no window to come back to (#48)
    decide "About Shapeshifter" with the version opens the settings, which show the name and version
      because the tray menu did not say whose icon it was (#586)
    decide "Report a problem…" opens a prefilled GitHub issue in the browser rather than sending a report itself
      because users could not find the version for a report, and automatic reports lacked what the user did (#342, #561)
    decide "Clear history" keeps pinned items and the current clipboard
      because pinning is how people keep something on purpose (#205, #459)
    decide on Linux the icon is dark on a light panel and light on a dark one, and GNOME and Pantheon count as dark
      because their top bar stays dark in light mode too (commit 3863433)
    decide a copy from an app store leaves "Check for updates…" out of the menu
      because the store updates that copy, and the item could only open the GitHub releases page
      rejected opening the store's page instead, because the store already shows and installs updates by itself
    decide Shapeshifter keeps working without an icon on desktops that have no tray
      because the gesture does not need the tray
  copy
    tray.settings     "Settings…"
    tray.logs         "Open log folder"
    tray.clear        "Clear history"
    tray.updates      "Check for updates…"
    tray.report       "Report a problem…"
    tray.about        "About Shapeshifter {version}"     e.g. "About Shapeshifter 7.1.0"
    tray.quit         "Quit Shapeshifter"
    tray.tooltip      "Shapeshifter {version}"
  uses      ProblemReport, ChangeSettings
  children  ProblemReport
  sources
    issues #48, #205, #261 "Add tray icon option for showing the log file", #283, #342, #459, #561, #586
    commits 741b0e5 "Add About, Report a problem and Copy version", 3863433 "Match the Linux tray icon to light and dark panels"
    driven on Linux (Xvfb, openbox, xfce4-panel tray) 2026-11-20: the menu (screenshot), each item, the tooltip "Shapeshifter 7.1.0"; links recorded by a stand-in for the browser
    src/Shapeshifter.Desktop/App.axaml.cs, CreateTrayIcon, TrayIconName, LinuxPanelIsDark, CheckForUpdates, ReportProblem
    src/Shapeshifter.Core/Localization/Strings.resx, Tray*
```
