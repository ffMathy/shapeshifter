# The action catalog

Every action, the order they are listed in, which items each applies to, and
what each one does.

```ail
node ActionCatalog : fn   level: system
  why   The list the user moves through by position must be the same on every
        platform and for every build, so which actions apply and their order
        are exact rules, testable without a desktop.   <- Actions
  what
    type IClipboardAction = { Id: text, Order: integer, GetTitle(item: ClipboardItem) -> text, GetDescription(item: ClipboardItem) -> text, CanExecute(item: ClipboardItem) -> boolean, ExecuteAsync(item: ClipboardItem, context: ActionContext) -> later nothing }
    ActionCatalog(actions: list of IClipboardAction) -> ActionCatalog
    ActionCatalog.All: list of IClipboardAction — the actions sorted by Order
    ActionCatalog.GetAvailable(item: ClipboardItem) -> list of IClipboardAction — the actions of All whose CanExecute(item) is true, in order
    ActionCatalog.CreateDefault() -> ActionCatalog
    ActionContext.WriteAndPasteAsync(snapshot: ClipboardSnapshot) -> later nothing — writes the clipboard, waits PasteSettleDelay = 40 ms, then simulates Ctrl+V (Cmd+V on macOS)
    ActionContext.CopyNewContentAsync(snapshot: ClipboardSnapshot) -> later nothing — writes the clipboard and adds the snapshot to the history as its newest item; no paste
    the catalog, one action per line:
              id                     order  offered when
              paste                  0      always
              paste-as-file          10     an image with bytes, or Text kind with non-empty text
              paste-plain-text       20     Text kind with a rich format (HTML or RTF), and either non-empty text or an HTML format
              paste-as-markdown      22     Text kind with an HTML format that converts to Markdown holding something (PasteAsMarkdown)
              paste-trimmed          24     Text kind whose text starts or ends with white space and holds something else (PasteTrimmed)
              upload-image           30     an image with bytes (UploadImage)
              upload-text            35     Text kind with text that is not only whitespace (UploadText)
              reformat-json          40     Text kind whose text is one JSON object or array (ReformatJson); titled Paste formatted JSON or Paste minified JSON
              decode-base64          42     Text kind whose text is Base64 of readable text (DecodeBase64)
              url-decode             44     Text kind whose text holds %-codes that decode to readable text (UrlDecode)
              paste-color-hex        46     Text kind whose text is one colour in rgb() or hsl() (ConvertColor)
              paste-color-rgb        47     Text kind whose text is one colour as hex or in hsl() (ConvertColor)
              paste-color-hsl        48     Text kind whose text is one colour as hex or in rgb() (ConvertColor)
              paste-as-date          49     Text kind whose text is a Unix time: 10 digits of seconds or 13 of milliseconds (ConvertTimestamp)
              paste-as-unix-time     50     Text kind whose text is an ISO 8601 date and time (ConvertTimestamp)
              paste-result           52     Text kind whose text is a calculation of numbers, + - * / % ^ and parentheses (PasteResult)
              paste-paths            70     File or FileCollection kind; titled Paste path for one file (PastePaths)
              paste-file-contents    72     File kind: one file of UTF-8 or UTF-16 text, at most 1 MB (PasteFileContents)
              paste-file-as-image    74     File kind: one PNG, JPEG, GIF, BMP, WebP or TIFF file, at most 25 MB (PasteFileAsImage)
              paste-as-zip           75     File or FileCollection kind
              copy-image-from-link   100    Text kind whose text holds a link to an image
              copy-file-at-path      110    Text kind whose text is the absolute path of an existing local file or folder (FilePath)
              open-links             200    Text kind whose text holds at least one link; opens at most 10
              write-email            210    Text kind whose text is one e-mail address, maybe after mailto: (WriteEmail)
              search-web             220    Text kind whose text is one line of at most 200 characters with a letter or digit, and not a link on its own (SearchWeb)
              show-path-in-file-manager 230    Text kind whose text is the absolute path of an existing local file or folder (FilePath)
              toggle-pin             255    always; titled "Unpin from clipboard" when the item is pinned
    examples
      the ids of CreateDefault().All, in order => [paste, paste-as-file, paste-plain-text, paste-as-markdown, paste-trimmed, upload-image, upload-text, reformat-json, decode-base64, url-decode, paste-color-hex, paste-color-rgb, paste-color-hsl, paste-as-date, paste-as-unix-time, paste-result, paste-paths, paste-file-contents, paste-file-as-image, paste-as-zip, copy-image-from-link, copy-file-at-path, open-links, write-email, search-web, show-path-in-file-manager, toggle-pin]
      GetAvailable(text "hello") => [paste, paste-as-file, upload-text, search-web, toggle-pin]
      GetAvailable(text "   ") => [paste, paste-as-file, toggle-pin]
      GetAvailable(text "  hello\n") => [paste, paste-as-file, paste-trimmed, upload-text, search-web, toggle-pin]
      GetAvailable(text "x" + raw "text/html") => [paste, paste-as-file, paste-plain-text, upload-text, search-web, toggle-pin]
      GetAvailable(raw "text/html" only, no plain text) => [paste, paste-plain-text, toggle-pin]
      GetAvailable(raw "Rich Text Format" only, no plain text) => [paste, toggle-pin]
      GetAvailable(PNG image) => [paste, paste-as-file, upload-image, toggle-pin]
      GetAvailable(image + one temporary image file, as Firefox copies) => [paste, paste-as-file, upload-image, toggle-pin]
      GetAvailable(text "chart" + image, as office suites copy) => [paste, paste-as-file, upload-text, search-web, toggle-pin]; paste-as-file and upload-text use the text
      GetAvailable(files ["/a/x.txt"]) => [paste, paste-as-zip, toggle-pin]
      GetAvailable(raw "application/x-weird" only) => [paste, toggle-pin]
      GetAvailable(text "see https://example.com/a.png and https://example.org") => [paste, paste-as-file, upload-text, copy-image-from-link, open-links, search-web, toggle-pin]
      GetAvailable(text "aGVsbG8gd29ybGQ=") => [paste, paste-as-file, upload-text, decode-base64, search-web, toggle-pin]
      GetAvailable(text "a%20b") => [paste, paste-as-file, upload-text, url-decode, search-web, toggle-pin]
      GetAvailable(text "{\"a\":1}") => [paste, paste-as-file, upload-text, reformat-json, search-web, toggle-pin]
      GetAvailable(text "#3584e4") => [paste, paste-as-file, upload-text, paste-color-rgb, paste-color-hsl, search-web, toggle-pin]
      GetAvailable(text "1795703400") => [paste, paste-as-file, upload-text, paste-as-date, search-web, toggle-pin]
      GetAvailable(text "2026-11-26T14:30:00Z") => [paste, paste-as-file, upload-text, paste-as-unix-time, search-web, toggle-pin]
      GetAvailable(text "12*7+3") => [paste, paste-as-file, upload-text, paste-result, search-web, toggle-pin]
      GetAvailable(text "anna@example.com") => [paste, paste-as-file, upload-text, write-email, search-web, toggle-pin]
      open-links.GetTitle(text "https://example.org") => "Open link";  with two links => "Open links"
      paste-as-file.GetDescription(JPEG image) => "Saves this image to a .jpg file and pastes the file."
      paste: ExecuteAsync(item) => WriteAndPasteAsync(item.Snapshot), then History.MoveToTop(item.Id) (no move for a pinned item)
      paste-plain-text: no plain text + HTML "<span>Hello <b>bold</b> <a href=\"x\">world</a></span>" => pastes { Text: "Hello bold world" }
      paste-as-file: text item captured 2026-11-20 12:57:21 local => <temp>/Text files/<id N>/Text 2026-11-20 12.57.21.txt, UTF-8 with a byte order mark, pasted as { Files: [that path] }
      paste-as-file: JPEG image => <temp>/Image files/<id N>/Image <time>.jpg
      toggle-pin, unpinned => PinnedItems.SaveAsync(item), then History.SetPinned(id, true)
      toggle-pin, pinned => History.SetPinned(id, false), then PinnedItems.DeleteAsync(id)
      paste-as-zip(["/a/x.txt", "/a/y.txt"]) => <temp>/Compressed files/<8 hex>/a.zip, pasted as { Files: [that path] } with WriteAndPasteAsync, not added to the history;  one file "/a/b.txt" => b.zip;  parents differ => Archive.zip
  how
    decide the gesture refers to an action by its index in the item's list, computed when the item is added and cached per item
      because the list must be known on the keyboard hook's thread without work; titles are made when shown, so Pin/Unpin follows the item
    decide an action is recognised by its Id, never by its title
      because titles are translated into 21 languages (docs/architecture.md, "Languages")
    decide temp is %TEMP%\Shapeshifter (Windows, macOS), or on Linux $XDG_RUNTIME_DIR/shapeshifter, else <data>/temp; deleted when the app starts
      because /tmp on Linux is shared with other users, and the files hold copied content
    decide a failing action is logged and the gesture still ends; the user sees nothing
      because "unknown, confirm with the owner: should a failed action tell the user?"
  sources
    src/Shapeshifter.Core/Actions/*.cs; src/Shapeshifter.Core/Clipboard/ImageFormat.cs; src/Shapeshifter.Core/Clipboard/ClipboardSnapshot.cs, Kind
    src/Shapeshifter.Core/Engine/ShapeshifterEngine.cs, CreateSession, PerformAsync and ProcessEffectsAsync
    src/Shapeshifter.Core/Settings/AppPaths.cs, TempDirectory
    driven on Linux 2026-11-20: the actions of plain text, HTML text, a PNG, files, other data and links; the Text and Image files written on disk
```
