# Write an e-mail

"Write email" on a copied e-mail address opens a new message to it in the
user's e-mail app.

```ail
node WriteEmail : fn   level: system
  why   An address copied from a web page, a signature or a document is
        usually copied to write to it; opening the mail app, starting a message
        and pasting it takes three steps.   <- Actions
  what
    WriteEmailAction is an IClipboardAction with Id "write-email" and Order 210:
      CanExecute(item) is true when item.Kind is Text and WriteEmailAction.ParseAddress(item.Snapshot.Text) gives an address
      GetTitle(item) is Strings.ActionWriteEmail, "Write email"
      GetDescription(item) is Localizer.Format(Strings.ActionWriteEmailDescription, the address), "Opens a new email to {0}."; Strings.ActionWriteEmailDescription with {0} as it is when there is none
      ExecuteAsync opens the link "mailto:" followed by the address with context.Launcher.OpenUrl, and pastes nothing
    WriteEmailAction.ParseAddress(text: text or nothing) -> text or nothing
      the text, without the white space at its start and end and without a leading "mailto:" in any letter case, must be one address and nothing else:
        at most 254 characters; exactly one @; before it 1 to 64 characters among letters, digits and . _ % + - ' (not starting or ending with a dot, no two dots in a row)
        after it a domain: labels of letters, digits and -, joined by single dots, at least two labels, the last one at least two letters; letters may be any Unicode letters
      gives the address as written, otherwise nothing
    examples
      ParseAddress("anna@example.com") => "anna@example.com"
      ParseAddress("  mailto:Anna.Berg+news@mail.example.co.uk ") => "Anna.Berg+news@mail.example.co.uk"
      ParseAddress("søren@eksempel.dk") => "søren@eksempel.dk"; any letters
      ParseAddress("anna@localhost") => nothing; one label
      ParseAddress("anna@example.c") => nothing; the last label is too short
      ParseAddress("anna@@example.com") => nothing
      ParseAddress(".anna@example.com") => nothing
      ParseAddress("write to anna@example.com") => nothing; only an address on its own
      ParseAddress("anna@example.com, bo@example.com") => nothing; one address
      CanExecute(text "anna@example.com") => true;  GetDescription => "Opens a new email to anna@example.com."
      CanExecute(PNG image) => false
      ExecuteAsync(text "anna@example.com") => opens mailto:anna@example.com; nothing is pasted, the clipboard and the history are unchanged
  how
    decide only an address on its own
      because which address of a longer text the user meant would be a guess, and the action would show on every e-mail signature copied
    decide a mailto: link opened by the system
      because it starts a message in whatever e-mail app the user has chosen, desktop or web, without Shapeshifter knowing which
    decide the description names the address
      because the user sees whom the message is to before letting go of Ctrl (#62)
  uses      ClipboardAction, ClipboardItem, Launcher, Localization
  sources
    the owner's request in this change: "R11 Write email"
```
