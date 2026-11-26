# URL-decode

"Paste URL-decoded" pastes a text with its %-codes, such as %20 or %C3%A6,
turned back into the characters they stand for.

```ail
node UrlDecode : fn   level: system
  why   Links copied from a browser's address bar, logs and redirects carry
        their words as %-codes, which are unreadable in a message or a
        document and had to be decoded by hand or on a website.   <- Actions
  what
    UrlDecodeAction is an IClipboardAction with Id "url-decode" and Order 44:
      CanExecute(item) is true when item.Kind is Text and UrlDecodeAction.TryDecode(item.Snapshot.Text) gives a text
      GetTitle(item) is Strings.ActionUrlDecode, "Paste URL-decoded"
      GetDescription(item) is Strings.ActionUrlDecodeDescription, "Pastes this text with its %-codes turned back into characters."
    UrlDecodeAction.MaxLength: integer = 1048576 — longer texts are not looked at
    UrlDecodeAction.TryDecode(text: text or nothing) -> text or nothing
      gives nothing for nothing, for a text longer than MaxLength, and for a text without a %-code: a % followed by two hexadecimal digits, in either case
      replaces each run of %-codes by the bytes they give, read as UTF-8; every other character, + included, stays as it is
      gives nothing when any run of bytes is not valid UTF-8, or when the result holds a control character other than tab, line feed and carriage return
      else gives the decoded text
    ExecuteAsync pastes ClipboardSnapshot { Text: the decoded text } with context.WriteAndPasteAsync; nothing is added to the history
    examples
      TryDecode("https://example.com/a%20b?q=%C3%A6%C3%B8") => "https://example.com/a b?q=æø"
      TryDecode("name%3Dvalue%26x%3d1") => "name=value&x=1"; lower-case hexadecimal digits work too
      TryDecode("a+b%21") => "a+b!"; + is not a space
      TryDecode("50% off") => nothing; no %-code
      TryDecode("100%") => nothing
      TryDecode("%zz") => nothing
      TryDecode("%C3") => nothing; half a character is not UTF-8
      TryDecode("%00") => nothing; a control character
      TryDecode("100%25 sure") => "100% sure"
      TryDecode(nothing) => nothing
      CanExecute(text "a%20b") => true
      CanExecute(text "hello world") => false
      ExecuteAsync(text "a%20b") => pastes { Text: "a b" }; the history is unchanged
  how
    decide + is left as it is
      because only web forms use + for a space; in a link's path and in most other text + is a +, and turning it into a space would change the text
    decide offered only when the text holds a %-code and decodes to readable UTF-8
      because a text with a stray % (like "50% off") has nothing to decode, and an action that would paste the same as Paste is noise
    decide the whole text is decoded, not only the links in it
      because the %-codes the user wants gone can be anywhere: a log line, a file name, a query on its own
    decide texts longer than MaxLength are not looked at
      because which actions an item offers is worked out when it is copied, where nothing slow may happen
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R4 URL-decode"
```
