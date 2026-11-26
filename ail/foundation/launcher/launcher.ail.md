# Launcher

```ail
node Launcher : data   level: system   @existing
  why   Some actions open something outside Shapeshifter: a link in the
        browser, a new e-mail.   <- Foundation
  what
    ActionContext.Launcher.OpenUrl(link: an absolute link) -> nothing
      opens an http, https or mailto link with the system's default app for it, at once; it returns before that app has started, and only logs when the system cannot open it
      any other kind of link throws ArgumentException
      in checks, ActionTestHarness gives the context a recording launcher: harness.Launcher.OpenedUrls lists, in order, the links opened (a list of absolute links), and nothing is opened for real
  sources
    src/Shapeshifter.Core/Platform/ISystemServices.cs, ILauncher; src/Shapeshifter.Platform/Common/Launcher.cs
```
