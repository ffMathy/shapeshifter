# Catbox

Anonymous uploads to Catbox (catbox.moe), the free file host both upload
actions use. It returns the public link of what it uploaded.

```ail
node Catbox : fn   level: system
  why   An image or a text is shared by pasting a public link to it, and
        getting one should not need an account or a separate tool
        (#23, #56).   <- Actions
  what
    Catbox.Name: text = "Catbox"
    Catbox.MaxBytes: integer = 200 × 1024 × 1024   — 200 MB
    Catbox.Endpoint: text = "https://catbox.moe/user/api.php"
    Catbox.Timeout: duration = 2 minutes   — how long an upload may take; a check may set it shorter, and sets it back after
    Catbox.UploadAsync(http: HttpClient, data: bytes, fileName: text, mediaType: text, cancellation) -> later text   — the public link, an absolute https address
    An upload is one POST to Endpoint, multipart/form-data: reqtype = "fileupload"; fileToUpload = data, named fileName, with mediaType as its content type
      mediaType may carry parameters, such as "text/plain; charset=utf-8", and is sent exactly as given
      the shared HttpClient sends User-Agent "Shapeshifter/<major.minor.patch>"; no account, no key
    The answer's body, without surrounding whitespace, is the link when the status is a success and the body is an absolute https address
    examples
      data of 3 bytes, "a.png", "image/png"; Catbox answers 200 "https://files.catbox.moe/abc123.png\n" => "https://files.catbox.moe/abc123.png"
      "a.txt" with mediaType "text/plain; charset=utf-8" => the file's content type is sent as text/plain; charset=utf-8
      Catbox answers 412 "File too large" => HttpRequestException "Catbox refused the upload (412): File too large", with status code 412
      Catbox answers 500 "https://files.catbox.moe/a.png" => HttpRequestException "Catbox refused the upload (500): https://files.catbox.moe/a.png"
      Catbox answers 200 "error: something" => HttpRequestException "Catbox did not return a link: error: something"
      Catbox answers 200 "http://files.catbox.moe/a.png" => HttpRequestException "Catbox did not return a link: http://files.catbox.moe/a.png"
      Catbox answers 200 "" => HttpRequestException "Catbox did not return a link: "
      an answer body of 250 characters in an error => the message shows its first 200 characters, then "…"
      data of 201 MB => InvalidOperationException "The file is larger than the 200 MB Catbox accepts.", and no request is sent
      Catbox.Timeout set to 200 ms, and no answer => the upload is cancelled with OperationCanceledException after about 200 ms
  how
    decide Catbox
      because it is free, has no ads, and takes anonymous uploads without an account (PR #629); the owner chose it for text as well (#56)
      rejected paste.gg, paste.rs, GitHub Gist and Pastebin for text, because the owner wanted one host for every upload; Gist and Pastebin also need an account or a key
    decide the size is checked before anything is sent
      because Catbox refuses anything over 200 MB only after the whole upload
    decide at most Catbox.Timeout, 2 minutes, per upload
      because "unknown, confirm with the owner: a large image on a slow line may need more"
  sources
    src/Shapeshifter.Core/Actions/UploadActions.cs, Catbox (before it was generated); PR ffMathy/shapeshifter#656
```
