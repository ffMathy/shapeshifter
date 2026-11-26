# Decode Base64

"Paste decoded Base64" pastes the text that a copied Base64 string stands for.
It is offered only when the string decodes to readable text.

```ail
node DecodeBase64 : fn   level: system
  why   Tokens, configuration values, e-mail headers and API answers often
        carry text as Base64; reading it meant pasting it into a website or a
        terminal first, which also hands it to someone else.   <- Actions
  what
    DecodeBase64Action is an IClipboardAction with Id "decode-base64" and Order 42:
      CanExecute(item) is true when item.Kind is Text and DecodeBase64Action.TryDecode(item.Snapshot.Text) gives a text
      GetTitle(item) is Strings.ActionDecodeBase64, "Paste decoded Base64"
      GetDescription(item) is Strings.ActionDecodeBase64Description, "Decodes this Base64 and pastes the text it holds."
    DecodeBase64Action.MaxLength: integer = 1048576 — longer texts are not looked at
    DecodeBase64Action.TryDecode(text: text or nothing) -> text or nothing
      takes the text without the white space at its start and end, and without line breaks inside it
      gives nothing unless what is left is at least 8 and at most MaxLength characters long, and only uses the Base64 alphabet: A–Z, a–z, 0–9 and + and /, or - and _ in their place (the URL-safe variant), then at most two = at the end
      decodes it, adding the = padding when it is missing, and gives nothing if the length cannot be Base64 even with padding
      gives nothing unless the bytes are valid UTF-8 and the text holds no control characters other than tab, line feed and carriage return
      else gives the decoded text
    ExecuteAsync pastes ClipboardSnapshot { Text: the decoded text } with context.WriteAndPasteAsync; nothing is added to the history
    examples
      TryDecode("aGVsbG8gd29ybGQ=") => "hello world"
      TryDecode("aGVsbG8gd29ybGQ") => "hello world"; the missing padding is added
      TryDecode("  aGVsbG8g\nd29ybGQ=\n") => "hello world"; line breaks, as in e-mail, are skipped
      TryDecode("PDw_Pz4-") => "<<??>>"; the URL-safe alphabet
      TryDecode("w6bDuMOl") => "æøå"
      TryDecode("dGVzdA==") => "test"
      TryDecode("dGVzdA") => nothing; shorter than 8 characters
      TryDecode("hello world") => nothing; a space is not Base64
      TryDecode("username") => nothing; it decodes, but not to UTF-8
      TryDecode("AAECAwQF") => nothing; bytes 0 to 5 are control characters
      TryDecode("aGVsbG8=d29y") => nothing; = only at the end
      TryDecode(nothing) => nothing
      CanExecute(text "aGVsbG8gd29ybGQ=") => true
      CanExecute(PNG image) => false
      ExecuteAsync(text "aGVsbG8gd29ybGQ=") => pastes { Text: "hello world" }; the history is unchanged
  how
    decide offered only when the string decodes to readable UTF-8 text
      because many ordinary words and ids are valid Base64 too; checking that the result is text keeps the action off them
      rejected pasting binary results as a file because "not asked for; a later change can add it"
    decide at least 8 characters
      because shorter strings are mostly words that happen to decode, and Base64 of anything worth decoding is longer
    decide both the standard and the URL-safe alphabet, with or without padding
      because JSON Web Tokens and URLs use the URL-safe form without padding, and e-mail and most APIs the standard one
    decide texts longer than MaxLength are not looked at
      because which actions an item offers is worked out when it is copied, where nothing slow may happen
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R3 Decode Base64"
```
