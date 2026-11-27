# Paste an image file as an image

"Paste as image" pastes the picture inside one copied image file, for apps
that take pictures but not files.

```ail
node PasteFileAsImage : fn   level: system
  why   Chats, documents, slides and image editors often take a pasted picture
        but not a pasted file, and opening the image to copy it again takes
        an app and three steps.   <- Actions
  what
    PasteFileAsImageAction is an IClipboardAction with Id "paste-file-as-image" and Order 74:
      CanExecute(item) is true when item.Kind is File and PasteFileAsImageAction.ReadImage(the one path in item.Snapshot.Files) gives an image
      GetTitle(item) is Strings.ActionPasteFileAsImage, "Paste as image"
      GetDescription(item) is Strings.ActionPasteFileAsImageDescription, "Pastes the picture in this file instead of the file."
      ExecuteAsync reads the file again with ReadImage and pastes ClipboardSnapshot { Image: those bytes } with context.WriteAndPasteAsync; nothing is added to the history; when ReadImage now gives nothing, it throws InvalidOperationException and pastes nothing
    PasteFileAsImageAction.MaxBytes: integer = 26214400 (25 MB)
    PasteFileAsImageAction.ReadImage(path: text) -> bytes or nothing
      gives nothing for a network path (starting \\ or //), for a path that is not an existing file, and for a file larger than MaxBytes
      gives the file's bytes when they start as one of these image types do, whatever the file is called: PNG (89 50 4E 47 0D 0A 1A 0A), JPEG (FF D8 FF), GIF (47 49 46 38), BMP (42 4D), WebP (52 49 46 46, then 4 bytes, then 57 45 42 50), TIFF (49 49 2A 00 or 4D 4D 00 2A)
      else gives nothing
    examples
      ReadImage(a file holding a PNG's bytes) => those bytes
      ReadImage(a file named photo.txt holding a JPEG's bytes) => those bytes; the start counts, not the name
      ReadImage(a file named image.png holding "hello") => nothing
      ReadImage(a file holding the 4 bytes 89 50 4E 47) => nothing; too short for a PNG's start
      ReadImage(a folder's path) => nothing
      ReadImage(a file of 26214401 bytes starting as a PNG) => nothing; larger than MaxBytes
      CanExecute(files [a PNG file]) => true
      CanExecute(files [a PNG file, another PNG file]) => false; only one file
      CanExecute(PNG image) => false; already an image
      ExecuteAsync(files [a PNG file]) => pastes { Image: the file's bytes }; the history is unchanged
  how
    decide the image type is read from the file's first bytes, not its name
      because names lie (a .png that is a JPEG is common), and a file named .png holding something else must not paste as an image
    decide one file, at most 25 MB, of the types the clipboard can carry
      because several pictures cannot be pasted at once, and the clipboard and the apps receiving it handle PNG, JPEG, GIF, BMP, WebP and TIFF; Copy image from link uses the same limit
    decide the bytes are pasted as they are
      because the clipboard writer converts them to what each system expects, as it does for any copied image
    decide the file is read again when the action runs
      because it may have changed since it was copied
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "F4 Paste as image"
```
