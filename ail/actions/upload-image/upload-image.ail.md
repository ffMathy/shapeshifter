# Upload and paste link

"Upload and paste link" uploads a copied image to Catbox, a free image host,
and pastes a public link to it. It is the only action that sends anything off
the computer, so it is described here from the code and never run by a check.

```ail
node UploadImage : fn   level: system
  why   Sharing a screenshot in a chat, a ticket or a form that takes no
        images needs a link, and getting one used to mean a separate upload
        tool (#23 "Implement the Upload image action").   <- Actions
  what
    UploadImageAction is an IClipboardAction with Id "upload-image" and Order 30:
      CanExecute(item) is true when item.Kind is Image and item.Snapshot.Image is not empty
      GetDescription(item) gives "Uploads this image to Catbox and pastes a public link to it."
    GetTitle(item) gives Strings.ActionUploadImage, "Upload and paste link"
    GetDescription(item) is Localizer.Format(Strings.ActionUploadImageDescription, Catbox.Name)
    ExecuteAsync uploads with Catbox.UploadAsync(context.Http, the image, "Image <yyyy-MM-dd HH.mm.ss>.<extension>", ImageFormat.GetMediaType(image)), the time being CapturedAt in local time and the extension ImageFormat.GetExtension(image)
      then pastes a snapshot holding only the link's text with context.WriteAndPasteAsync, then adds that snapshot with context.History.Add
      and logs "Uploaded an image to {Service}" with Catbox.Name
    examples
      ExecuteAsync(PNG captured 2026-11-20 13:05:09 local) => uploads "Image 2026-11-20 13.05.09.png" as image/png
      ExecuteAsync(JPEG image) => uploads "Image <time>.jpg" as image/jpeg
      Catbox answers 200 "https://files.catbox.moe/abc123.png" => pastes the text "https://files.catbox.moe/abc123.png", and adds it to the history as its newest item
      ExecuteAsync(the same item again) => no second upload; the link remembered for that item is pasted again
      CanExecute(text "hello") => false;  CanExecute(empty image) => false
      Catbox answers 412 "File too large" => HttpRequestException; nothing pasted, nothing added to the history
  how
    decide Catbox
      because it is free, has no ads, and takes anonymous uploads without an account (PR #629)
    decide upload straight away, without asking first
      because the first-run consent dialog took the keyboard focus, so the first upload only copied the link instead of pasting it (PR #641)
      rejected a consent dialog remembered in the settings, the first version (PR #629, commit 9a08207), for that reason
    decide the link is pasted, then added to the history
      because "unknown, confirm with the owner: so the link can be pasted again later?"
    decide each item is uploaded at most once while Shapeshifter runs; the remembered link is lost on restart
      because "unknown, confirm with the owner: to avoid a second public copy, or only to save time?"
    decide an upload cannot be deleted from Shapeshifter, and anyone with the link can see it
      because Catbox's anonymous uploads have no owner (PR #629 told users so in its consent dialog); README.md says what is uploaded, under "Nothing you copy leaves your computer"
    decide a failure is logged and nothing is pasted; the user is not told
      because "unknown, confirm with the owner: should a failed upload show a notification?"
  uses  Catbox, ClipboardAction, ClipboardItem, ActionContext, ImageFormat, Localization
  sources
    GitHub issue #23; PRs #629 "Image actions (paste as file, upload to Catbox)", #641 "Upload images to Catbox without asking for consent first"
    commits 9a08207, 37b802e; README.md, "Good to know"; docs/architecture.md, "Pasting"
    src/Shapeshifter.Core/Actions/UploadActions.cs; src/Shapeshifter.Core/Clipboard/ImageFormat.cs
    src/Shapeshifter.Desktop/App.axaml.cs, StartAsync: the HttpClient and its User-Agent
    not driven: an upload sends the image to a public host; seen in the picker only as an offered action (driven on Linux 2026-11-20)
```
