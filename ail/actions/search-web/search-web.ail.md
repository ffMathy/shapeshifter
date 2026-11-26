# Search the web

"Search the web" on a short copied text opens a web search for it in the
browser.

```ail
node SearchWeb : fn   level: system
  why   A word, a name, an error message or a product number is often copied
        to look it up; opening the browser and pasting it into a search takes
        three steps.   <- Actions
  what
    SearchWebAction is an IClipboardAction with Id "search-web" and Order 220:
      CanExecute(item) is true when item.Kind is Text and SearchWebAction.SearchUrl(item.Snapshot.Text) gives a link
      GetTitle(item) is Strings.ActionSearchWeb, "Search the web"
      GetDescription(item) is Localizer.Format(Strings.ActionSearchWebDescription, SearchWebAction.EngineName), "Searches {0} for this text in your browser."
      ExecuteAsync opens SearchUrl(the text) with context.Launcher.OpenUrl, and pastes nothing
    SearchWebAction.EngineName: text = "DuckDuckGo"
    SearchWebAction.MaxLength: integer = 200
    SearchWebAction.SearchUrl(text: text or nothing) -> an absolute link or nothing
      takes the text without the white space at its start and end, and gives nothing when that is empty, longer than MaxLength characters, holds a line break, holds no letter or digit, or is a link on its own (http:// or https:// followed by anything without spaces)
      else gives "https://duckduckgo.com/?q=" followed by the text, encoded as a URI component (a space as %20)
    examples
      SearchUrl("shapeshifter clipboard") => https://duckduckgo.com/?q=shapeshifter%20clipboard
      SearchUrl("  NullReferenceException  ") => https://duckduckgo.com/?q=NullReferenceException
      SearchUrl("C# & .NET?") => https://duckduckgo.com/?q=C%23%20%26%20.NET%3F
      SearchUrl("æble") => https://duckduckgo.com/?q=%C3%A6ble
      SearchUrl("two\nlines") => nothing
      SearchUrl("   ") => nothing
      SearchUrl("!!!") => nothing; no letter or digit
      SearchUrl("https://example.com/page") => nothing; Open link opens it
      SearchUrl(a text of 201 characters) => nothing
      CanExecute(text "hello world") => true;  GetDescription => "Searches DuckDuckGo for this text in your browser."
      CanExecute(PNG image) => false
      ExecuteAsync(text "hello world") => opens https://duckduckgo.com/?q=hello%20world; nothing is pasted, the clipboard and the history are unchanged
  how
    decide DuckDuckGo
      because it does not tie the search to an account or profile the user, which suits an app whose rule is that nothing copied leaves the computer unless the user asks; "unknown, confirm with the owner: a setting to choose the search engine?"
      rejected the browser's own default search engine because no system tells an app which one it is
    decide only a single line of at most 200 characters, with a letter or digit, that is not a link on its own
      because longer texts and texts over several lines are rarely searched as they are, and a link is opened by Open link instead
    decide the description names the search engine
      because the text leaves the computer, and the user should see where it goes before letting go of Ctrl
  uses      ClipboardAction, ClipboardItem, Launcher, Localization
  sources
    the owner's request in this change: "R12 Search the web"
```
