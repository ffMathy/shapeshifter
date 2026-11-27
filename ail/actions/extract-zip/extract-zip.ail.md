# Paste extracted

"Paste extracted" pastes what is inside one copied .zip file: its files and
folders, unpacked, instead of the archive.

```ail
node ExtractZip : fn   level: system
  why   Archives arrive by e-mail and download and are usually only wanted
        unpacked; unpacking means opening the archive in an app and choosing
        where its contents go. Paste as compressed file is the way in; this is
        the way out.   <- Actions
  what
    ExtractZipAction is an IClipboardAction with Id "extract-zip" and Order 76:
      CanExecute(item) is true when item.Kind is File and ExtractZipAction.IsZip(the one path in item.Snapshot.Files)
      GetTitle(item) is Strings.ActionExtractZip, "Paste extracted"
      GetDescription(item) is Strings.ActionExtractZipDescription, "Unpacks this .zip file and pastes the files inside it."
      ExecuteAsync extracts with ExtractZipAction.Extract(the path, context.Paths.TempDirectory), then pastes ClipboardSnapshot { Files: what it gives } with context.WriteAndPasteAsync; nothing is added to the history
    ExtractZipAction.IsZip(path: text) -> boolean
      true when the path is not a network path (starting \\ or //), names an existing file whose name ends in .zip in any letter case, and whose first four bytes are 50 4B 03 04 or 50 4B 05 06 (an empty archive)
    ExtractZipAction.MaxUnpackedBytes: integer = 2147483648 (2 GB)
    ExtractZipAction.Extract(zip: text, temp: text) -> list of text
      unpacks the archive into a new folder <temp>/Extracted files/<8 random hexadecimal digits>/<the archive's name without .zip>/
      gives the full paths of the entries directly in that folder, files and folders, ordered by name, ordinal and ignoring case
      an entry whose path would end up outside that folder (.. or an absolute path in its name) throws InvalidDataException before anything is written
      an archive whose entries together unpack to more than MaxUnpackedBytes throws InvalidDataException before anything is written
      an archive that cannot be read throws InvalidDataException
      an empty archive gives an empty list, and then nothing is pasted
    examples
      IsZip(an existing file "photos.zip" made by a zip tool) => true
      IsZip(an existing file "PHOTOS.ZIP") => true
      IsZip(an existing file "photos.zip" holding "hello") => false; not an archive
      IsZip(an existing archive named "photos.bin") => false; the name must end in .zip
      IsZip(the path of nothing) => false
      Extract(an archive "trip.zip" holding "a.jpg", "b.jpg" and "notes/day1.txt", temp "/tmp/s") => ["/tmp/s/Extracted files/<8 hex>/trip/a.jpg", "/tmp/s/Extracted files/<8 hex>/trip/b.jpg", "/tmp/s/Extracted files/<8 hex>/trip/notes"]; notes/day1.txt is inside the notes folder
      Extract(an archive holding an entry "../evil.txt") => InvalidDataException; nothing is written
      CanExecute(files [an archive "trip.zip"]) => true
      CanExecute(files [an archive, another archive]) => false; only one file
      CanExecute(text "trip.zip") => false
      ExecuteAsync(files ["trip.zip" holding "a.jpg"]) => pastes { Files: ["<temp>/Extracted files/<8 hex>/trip/a.jpg"] }; the history is unchanged
  how
    decide the top-level entries are pasted, inside a folder named after the archive
      because pasting into a file manager then gives the files themselves, while the folder keeps two archives with the same file names apart
    decide only .zip files, recognised by name and by their first bytes
      because .zip is what the system and Paste as compressed file make; other formats need tools Shapeshifter does not have
    decide unsafe entry paths and archives over 2 GB unpacked are refused before anything is written
      because a crafted archive could otherwise write outside the folder or fill the disk
    decide the files go in Shapeshifter's temporary folder
      because Paste as compressed file and Paste as file use it too, and it is emptied the next time Shapeshifter starts
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, TempFolder, Localization
  sources
    the owner's request in this change: "F5 Extract .zip"
```
