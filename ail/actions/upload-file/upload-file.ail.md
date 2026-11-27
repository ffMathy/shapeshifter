# Upload file and paste link

"Upload and paste link" on one copied file uploads it to Catbox and pastes a
public link to it, as the action already does for an image or a text.

```ail
node UploadFile : fn   level: system
  why   A file too large for an e-mail or a chat, or one to hand to someone
        outside the company, is shared by a link; getting one meant a file
        sharing website and an account (#56 asked for the same for text).   <- Actions
  what
    UploadFileAction is an IClipboardAction with Id "upload-file" and Order 78:
      CanExecute(item) is true when item.Kind is File and the one path in item.Snapshot.Files is not a network path (starting \\ or //) and names an existing file, not a folder, of 1 byte to Catbox.MaxBytes
      GetTitle(item) is Strings.ActionUploadText, "Upload and paste link", the title UploadImage and UploadText show
      GetDescription(item) is Localizer.Format(Strings.ActionUploadFileDescription, Catbox.Name), "Uploads this file to {0} and pastes a public link to it."
    UploadFileAction.MediaType: text = "application/octet-stream"
    ExecuteAsync reads the file and uploads it with Catbox.UploadAsync(context.Http, its bytes, the file's own name, MediaType)
      then pastes a snapshot holding only the link's text with context.WriteAndPasteAsync, then adds that snapshot with context.History.Add
      and logs "Uploaded a file to {Service}" with Catbox.Name
    examples
      CanExecute(files [an existing file "report.pdf" of 3 bytes]) => true
      CanExecute(files [an existing empty file]) => false
      CanExecute(files [a folder]) => false
      CanExecute(files [a file, another file]) => false; one file only
      CanExecute(files [the path of nothing]) => false
      CanExecute(files ["\\\\server\\share\\a.txt"]) => false; a network path
      CanExecute(text "report.pdf") => false
      GetTitle(files [an existing file]) => "Upload and paste link"
      ExecuteAsync(files ["…/report.pdf" holding 01 02 03]) => uploads "report.pdf" as application/octet-stream holding 01 02 03
      Catbox answers 200 "https://files.catbox.moe/abc123.pdf" => pastes the text "https://files.catbox.moe/abc123.pdf", and adds it to the history as its newest item
      ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again
      Catbox answers 412 "Invalid uploader" => HttpRequestException; nothing pasted, nothing added to the history
  how
    decide Catbox, as for images and text
      because the owner chose one host for every upload: anonymous, free and without an account
    decide one file of at most Catbox.MaxBytes, never a folder
      because Catbox takes one file per link and nothing over 200 MB, and a folder would first need Paste as compressed file
    decide the file keeps its own name, sent as application/octet-stream
      because Catbox keeps the extension in the link and serves each type itself; guessing a type could only be wrong
    decide the same title as the other uploads, with its own description
      because an item is a file, an image or a text, so two uploads never appear in one list
    decide each item is uploaded at most once while Shapeshifter runs, a failure is logged and nothing is pasted, and an upload cannot be deleted
      because UploadImage and UploadText do the same, for the reasons they record
  uses      Catbox, ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "F6 Upload file and paste link"
    not driven: an upload sends the file to a public host
```
