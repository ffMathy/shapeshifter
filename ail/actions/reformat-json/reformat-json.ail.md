# Format or minify JSON

A copied JSON object or array can be pasted indented, one value per line, or
on one line without spaces: "Paste formatted JSON" for JSON that is on one
line, "Paste minified JSON" for JSON that is spread over several.

```ail
node ReformatJson : fn   level: system
  why   JSON copied from an API answer, a log or a browser's developer tools is
        either one unreadable line or spread over many lines where one is
        wanted, as in a command, a test or a configuration value; reshaping it
        meant pasting it into a website or an editor first.   <- Actions
  what
    ReformatJsonAction is an IClipboardAction with Id "reformat-json" and Order 40:
      CanExecute(item) is true when item.Kind is Text and ReformatJsonAction.Parse(item.Snapshot.Text) succeeds
      GetTitle(item) is Strings.ActionFormatJson, "Paste formatted JSON", when the text, without the white space at its start and end, is on one line; else Strings.ActionMinifyJson, "Paste minified JSON"
      GetDescription(item) is Strings.ActionFormatJsonDescription, "Pastes this JSON indented, one value per line.", or Strings.ActionMinifyJsonDescription, "Pastes this JSON on one line, without spaces.", following the title
    ReformatJsonAction.MaxLength: integer = 1048576 — longer texts are not looked at
    ReformatJsonAction.Parse(text: text or nothing) succeeds when the text is at most MaxLength characters long and, without the white space at its start and end, is one JSON object or array in strict JSON: no comments, no trailing commas, nothing after it
    ReformatJsonAction.Format(text: text) -> text
      every object member and array element on its own line, indented by two spaces per level, a space after each colon, line feeds between lines, nothing after the last line
      an empty object or array stays {} or []
    ReformatJsonAction.Minify(text: text) -> text
      the same values with no white space between them
    both keep the members in their order, repeated names included, and every number exactly as it was written
    both write strings with only the escapes JSON requires: \" and \\, and \n, \r, \t or \u00XX for control characters, its hex digits in upper case (U+001B as \u001B); every other character, such as æ, é, < and &, is written as itself
    ExecuteAsync pastes ClipboardSnapshot { Text: Format(the text) } when the title is "Paste formatted JSON", else ClipboardSnapshot { Text: Minify(the text) }, with context.WriteAndPasteAsync; nothing is added to the history
    examples
      Format("{\"a\":1,\"b\":[true,null,\"x\"]}") => "{\n  \"a\": 1,\n  \"b\": [\n    true,\n    null,\n    \"x\"\n  ]\n}"
      Format("{\"empty\":{},\"none\":[]}") => "{\n  \"empty\": {},\n  \"none\": []\n}"
      Format("[1.50,2e3]") => "[\n  1.50,\n  2e3\n]"; numbers stay as written
      Format("{\"name\":\"Søren & <co>\"}") => "{\n  \"name\": \"Søren & <co>\"\n}"
      Format("{\"a\":1,\"a\":2}") => "{\n  \"a\": 1,\n  \"a\": 2\n}"
      Minify("{\n  \"a\": 1,\n  \"b\": [ true, \"x y\" ]\n}") => "{\"a\":1,\"b\":[true,\"x y\"]}"
      Minify("[\"line\\nbreak\"]") => "[\"line\\nbreak\"]"
      CanExecute(text "{\"a\":1}") => true;  GetTitle => "Paste formatted JSON"
      CanExecute(text "  [1, 2]\n") => true;  GetTitle => "Paste formatted JSON"; white space around the text does not count
      CanExecute(text "{\n  \"a\": 1\n}") => true;  GetTitle => "Paste minified JSON"
      CanExecute(text "42") => false; only an object or an array
      CanExecute(text "\"just a string\"") => false
      CanExecute(text "{a: 1}") => false; names must be quoted
      CanExecute(text "{\"a\":1,}") => false; a trailing comma
      CanExecute(text "{\"a\":1} // note") => false; a comment
      CanExecute(text "{\"a\":1}{\"b\":2}") => false; two values
      CanExecute(text "hello") => false
      CanExecute(PNG image) => false
      ExecuteAsync(text "{\"a\":1}") => pastes { Text: "{\n  \"a\": 1\n}" }; the history is unchanged
      ExecuteAsync(text "{\n  \"a\": 1\n}") => pastes { Text: "{\"a\":1}" }
  how
    decide one action whose title follows the text: Format for JSON on one line, Minify for JSON over several
      because the user wants the other shape; offering the shape the text already has would be noise
    decide only objects and arrays
      because a lone number, string or literal such as true is valid JSON too, but there is nothing to reshape in it, and the action would be offered on every number copied
    decide strict JSON only
      because JSON with comments or trailing commas is a different language each tool reads its own way; reshaping it could drop what the user wrote
    decide two spaces, numbers and member order kept, characters other than the required escapes written as themselves
      because the result should differ from the original only in white space, and two spaces is what most formatters and APIs use
    decide texts longer than MaxLength are not looked at
      because which actions an item offers is worked out when it is copied, where nothing slow may happen
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R1 Format JSON / Minify JSON"
```
