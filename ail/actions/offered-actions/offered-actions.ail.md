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
    Scenario: formatted text also offers Paste as plain text
      Given a text with formatting (bold, from a web page) was copied
      When the user opens its actions
      Then they are "Paste", "Paste as file", "Paste as plain text", "Upload and paste link", "Pin to clipboard"
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
    decide one fixed order for every kind: Paste, Paste as file, Paste as plain text, Paste trimmed, Upload and paste link, Paste decoded Base64, Paste URL-decoded, Paste as compressed file, Copy image from link, Open link or Open links, then Pin or Unpin
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
