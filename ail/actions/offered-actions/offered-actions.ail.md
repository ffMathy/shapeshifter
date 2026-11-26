# Which actions an item offers

Each kind of item offers only the actions that make sense for it, always in
the same order: Paste first, Pin last.

```ail
node OfferedActions : flow   level: experience
  why   An action that cannot work on an item is noise in a list the user
        reads while holding keys; the list must be short and its order fixed,
        so the keys to reach an action can be learnt (#62).   <- Actions
  what
    Scenario: plain text offers Paste, Paste as file, Upload and paste link, and Pin
      Given a plain text was copied
      When the user holds Ctrl+V until the picker appears and taps Right
      Then the actions are "Paste", "Paste as file", "Upload and paste link", "Pin to clipboard", in that order
    Scenario: formatted text also offers Paste as plain text and Paste as Markdown
      Given a text with formatting (bold, from a web page) was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Paste as plain text", "Paste as Markdown", "Upload and paste link", "Pin to clipboard"
    Scenario: text with spaces or blank lines around it also offers Paste trimmed
      Given the text "  padded \n" was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Paste trimmed", "Upload and paste link", "Pin to clipboard"
    Scenario: Base64 of a readable text also offers Paste decoded Base64
      Given the Base64 of a readable text, such as "aGVsbG8gd29ybGQ=", was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Upload and paste link", "Paste decoded Base64", "Pin to clipboard"
    Scenario: a text with %-codes also offers Paste URL-decoded
      Given the text "a%20b" was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Upload and paste link", "Paste URL-decoded", "Pin to clipboard"
    Scenario: JSON on one line also offers Paste formatted JSON, and JSON over several lines Paste minified JSON
      Given the text "{\"a\":1}" was copied, and then the same JSON spread over three lines
      When the user opens the actions of each
      Then the first's are "Paste", "Paste as file", "Upload and paste link", "Paste formatted JSON", "Pin to clipboard"
      And the second's are "Paste", "Paste as file", "Upload and paste link", "Paste minified JSON", "Pin to clipboard"
    Scenario: a colour also offers it in the other notations
      Given the text "#3584e4" was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Upload and paste link", "Paste as RGB colour", "Paste as HSL colour", "Pin to clipboard"
    Scenario: a Unix time also offers Paste as date and time, and a date and time Paste as Unix time
      Given the text "1795703400" was copied, and then "2026-11-26T14:30:00Z"
      When the user opens the actions of each
      Then the first's are "Paste", "Paste as file", "Upload and paste link", "Paste as date and time", "Pin to clipboard"
      And the second's are "Paste", "Paste as file", "Upload and paste link", "Paste as Unix time", "Pin to clipboard"
    Scenario: an image offers Paste, Paste as file, Upload and paste link, and Pin
      Given an image was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Upload and paste link", "Pin to clipboard"
    Scenario: a file offers Paste, Paste as compressed file and Pin
      Given one file was copied in a file manager
      When the user opens its actions
      Then they are "Paste", "Paste as compressed file", "Pin to clipboard"
    Scenario: several files offer the same as one file
      Given two files were copied together
      When the user opens their actions
      Then they are "Paste", "Paste as compressed file", "Pin to clipboard"
    Scenario: other data offers only Paste and Pin
      Given something that is neither text, an image nor files was copied
      When the user opens its actions
      Then they are "Paste", "Pin to clipboard"
    Scenario: text with links also offers the link actions
      Given the text "see https://example.com/a.png and https://example.org" was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Upload and paste link", "Copy image from link", "Open links", "Pin to clipboard"
  how
    decide one fixed order for every kind: Paste, Paste as file, Paste as plain text, Paste as Markdown, Paste trimmed, Upload and paste link, Paste formatted JSON or Paste minified JSON, Paste decoded Base64, Paste URL-decoded, Paste as hex colour, Paste as RGB colour, Paste as HSL colour, Paste as date and time, Paste as Unix time, Paste as compressed file, Copy image from link, Open link or Open links, then Pin or Unpin
      because Paste is the default and comes first; "unknown, confirm with the owner: why this order for the rest"
    decide a pinned item offers "Unpin from clipboard" in place of "Pin to clipboard", in the same place
      because "unknown, confirm with the owner"
    decide the descriptions say what the action does with this item: ".txt" for text, the image's own type (".png", ".jpg") for an image, "this text" or "this image" for an upload, one link or several
      because each description is written for the item it is shown with (#62 "Have description of actions be dynamically generated")
    decide other data is headed "Actions for this text" in the Windows and Mac looks
      because "unknown, confirm with the owner: keep this?"
    decide which actions an item offers is worked out once, when it is copied
      because the list is needed on the keyboard hook's thread while the gesture runs, where nothing slow may happen
  copy
    action.paste            "Paste"                        "Pastes clipboard contents as-is."
    action.pasteFile        "Paste as file"                "Saves this text to a .txt file and pastes the file." / "Saves this image to a .{ext} file and pastes the file."
    action.plain            "Paste as plain text"          "Pastes clipboard contents as plain text with no formatting."
    action.trimmed          "Paste trimmed"                "Pastes this text without the spaces and blank lines at its start and end."
    action.upload           "Upload and paste link"        "Uploads this image to Catbox and pastes a public link to it." / "Uploads this text to Catbox and pastes a public link to it."
    action.zip              "Paste as compressed file"     "Compresses these files into one .zip file and pastes it."
    action.imageFromLink    "Copy image from link"         "Copies the linked image itself to the clipboard."
    action.openLink         "Open link" / "Open links"     "Opens the link in your browser." / "Opens the links in your browser (at most 10)."
    action.pin              "Pin to clipboard"             "Keeps this item at the top, saved for next time."
    action.unpin            "Unpin from clipboard"         "Lets this item move down, and stops saving it."
    action.base64           "Paste decoded Base64"           "Decodes this Base64 and pastes the text it holds."
    action.urlDecode        "Paste URL-decoded"              "Pastes this text with its %-codes turned back into characters."
    action.minifyJson       "Paste minified JSON"            "Pastes this JSON on one line, without spaces."
    action.formatJson       "Paste formatted JSON"           "Pastes this JSON indented, one value per line."
    action.markdown         "Paste as Markdown"              "Pastes this text as Markdown, keeping headings, lists, links and emphasis."
    action.colorHex         "Paste as hex colour"            "Pastes {0}."
    action.colorRgb         "Paste as RGB colour"            "Pastes {0}."
    action.colorHsl         "Paste as HSL colour"            "Pastes {0}."
    action.date             "Paste as date and time"         "Pastes {0}."
    action.unixTime         "Paste as Unix time"             "Pastes {0}."
    heading                 "Actions"   with "→ to choose" beside it in the Linux look; the Windows and macOS looks head the list "Actions for this text", "Actions for this image", "Actions for this file" or "Actions for these files" (other data, too, reads "Actions for this text")
    default                 "Default"   the badge on Paste in the Windows and macOS looks; a tick in the Linux look
  uses      ActionCatalog
  sources
    GitHub issues #62, #56; PR #629 "Image actions (paste as file, upload to Catbox) and a website Actions section"
    driven on Linux (Xvfb, openbox) 2026-11-20: plain text, HTML text, a PNG, one file, two files, a custom format and text with two links put on the clipboard with xclip; actions read from screenshots
    src/Shapeshifter.Core/Actions/ActionCatalog.cs, CreateDefault and GetAvailable
    src/Shapeshifter.Core/Localization/Strings.resx, Action* and PickerActionsFor*
    src/Shapeshifter.Desktop/ViewModels/PickerViewModel.cs, UpdateActionsHeader; src/Shapeshifter.Desktop/Styles/Linux.axaml, "→ to choose"
```
