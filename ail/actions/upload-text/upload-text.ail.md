# Upload text and paste link

"Upload and paste link" on a text uploads it to Catbox as a .txt file and
pastes a public link to it. Catbox's link is the file itself, so whoever opens
it sees the raw text, with nothing around it. Like UploadImage it sends what
was copied off the computer, so no check runs it.

```ail
node UploadText : fn   level: system
  why   Sharing a log, a snippet of code or a long message in a chat, a ticket
        or a form that cuts long text needs a link to the text itself, and
        getting one meant opening a pastebin and pasting it there first
        (#56 "Pastebin / github gists integration").   <- Actions
  what
    UploadTextAction is an IClipboardAction with Id "upload-text" and Order 35:
      CanExecute(item) is true when item.Kind is Text and item.Snapshot.Text holds something other than whitespace
      GetTitle(item) gives "Upload and paste link", the same title as UploadImage
      GetDescription(item) gives "Uploads this text to Catbox and pastes a public link to it."
    UploadTextAction.MediaType: text = "text/plain; charset=utf-8"
    the upload is Catbox.UploadAsync(http, the text, "Text <yyyy-MM-dd HH.mm.ss>.txt", MediaType), with UploadImage's endpoint, form fields, 200 MB limit, 2 minute timeout and answers
    the file holds the text in UTF-8, after a UTF-8 byte order mark
    examples
      ExecuteAsync(text "héllo wörld" captured 2026-11-20 13:05:09 local) => uploads "Text 2026-11-20 13.05.09.txt" as text/plain; charset=utf-8, holding EF BB BF then "héllo wörld" in UTF-8
      Catbox answers 200 "https://files.catbox.moe/abc123.txt" => pastes the text "https://files.catbox.moe/abc123.txt", and adds it to the history as its newest item
      ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again
      Catbox answers 412 "Invalid uploader" => HttpRequestException "Catbox refused the upload (412): Invalid uploader"; nothing pasted
      Catbox answers 200 "error: something" => HttpRequestException "Catbox did not return a link: error: something"
      CanExecute(text "   ") => false
      CanExecute(raw "text/html" only, no plain text) => false
      CanExecute(text "caption" + image, as office suites copy) => true; the text is uploaded, not the image
  how
    decide Catbox, the host UploadImage already uses
      because the owner chose it over paste.gg and paste.rs for #56: one host for every upload, anonymous and without an account
      rejected paste.gg, the first choice, because the owner preferred Catbox; it also deletes anonymous pastes after 7 days
      rejected GitHub Gist and Pastebin because both need an account or a key
    decide the link pasted is Catbox's own link to the file
      because it already is the raw text: there is no page around it to strip
    decide a byte order mark comes first
      because Catbox may announce no character set, and with the mark a browser still reads the text as UTF-8; paste-as-file writes its .txt the same way
    decide the same title as UploadImage, with its own description
      because an item is either a text or an image, so the two never appear in one list, and the description says which is uploaded
    decide text that is only whitespace offers no upload
      because a link to nothing visible is of no use to anyone
    decide formatted text uploads its plain text only
      because a .txt file shows the words; the formatting would show as markup
    decide each item is uploaded at most once while Shapeshifter runs, a failure is logged and nothing is pasted, and an upload cannot be deleted
      because UploadImage does the same, for the reasons it records
  uses      UploadImage
  sources
    GitHub issue #56 and its comments; the owner's answer in this change: Catbox, not paste.gg
    src/Shapeshifter.Core/Actions/UploadActions.cs, UploadTextAction
    not driven: an upload sends the text to a public host; from this change's sandbox Catbox answered "Invalid uploader" to every upload, images too
```
