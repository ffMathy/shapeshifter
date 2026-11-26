# Convert a colour

A copied colour, written as hex (#3584e4), rgb() or hsl(), can be pasted in
the other two notations: "Paste as hex colour", "Paste as RGB colour" and
"Paste as HSL colour", each offered only when the colour is in another one.

```ail
node ConvertColor : fn   level: system
  why   Design tools, style sheets, code and documents each want colours in
        their own notation, and converting one by hand or on a website takes
        longer than copying it did (#299 showed copied colours already).   <- Actions
  what
    type ColorNotation = "hex" | "rgb" | "hsl"
    type ParsedColor = { Red: integer, Green: integer, Blue: integer, Alpha: number, Notation: ColorNotation }
      Red, Green and Blue are 0 to 255, Alpha 0 to 1 (1 when the colour gives none)
    ConvertColor.Parse(text: text or nothing) -> ParsedColor or nothing
      the text, without the white space at its start and end, must be exactly one colour, in any letter case:
        hex: # and then 3, 4, 6 or 8 hexadecimal digits (#rgb, #rgba, #rrggbb, #rrggbbaa)
        rgb: rgb( or rgba(, three values 0–255 or percentages 0%–100%, and maybe an alpha; values separated by commas, or by spaces with the alpha after a /
        hsl: hsl( or hsla(, a hue in degrees (deg may follow it), a saturation and a lightness as percentages 0%–100%, and maybe an alpha, separated the same way
        an alpha is a number 0 to 1, or a percentage 0%–100%
      anything else, a value out of range included, gives nothing; so does nothing
      percentages and hsl() are turned into Red, Green and Blue by the CSS rules, each rounded to the nearest whole number, halves up
    ConvertColor.Format(color: ParsedColor, notation: ColorNotation) -> text
      hex: # and six lower-case hexadecimal digits, and two more for the alpha when it is below 1 (alpha × 255, rounded)
      rgb: "rgb(R, G, B)", or "rgba(R, G, B, A)" when the alpha is below 1
      hsl: "hsl(H, S%, L%)" with H, S and L rounded to whole numbers, or "hsla(H, S%, L%, A)" when the alpha is below 1
      A is the alpha with at most two decimals, without trailing zeros: 0.5, 0.25, 0
    PasteAsHexColorAction, PasteAsRgbColorAction and PasteAsHslColorAction are IClipboardActions with Ids "paste-color-hex", "paste-color-rgb", "paste-color-hsl" and Orders 46, 47, 48:
      CanExecute(item) is true when item.Kind is Text and Parse(item.Snapshot.Text) gives a colour whose Notation is not the action's own
      GetTitle(item) is Strings.ActionColorHex "Paste as hex colour", Strings.ActionColorRgb "Paste as RGB colour" or Strings.ActionColorHsl "Paste as HSL colour"
      GetDescription(item) is Localizer.Format(Strings.ActionColorDescription, Format(the colour, the action's notation)), "Pastes {0}."; when the item is no colour, Format is left out and the description is Strings.ActionColorDescription with {0} as it is
      ExecuteAsync pastes ClipboardSnapshot { Text: Format(the colour, the action's notation) } with context.WriteAndPasteAsync; nothing is added to the history
    examples
      Parse("#3584e4") => { Red: 53, Green: 132, Blue: 228, Alpha: 1, Notation: "hex" }
      Parse(" #FFF ") => { Red: 255, Green: 255, Blue: 255, Alpha: 1, Notation: "hex" }
      Parse("#3584e480") => Alpha 128/255, about 0.5
      Parse("rgb(53, 132, 228)") => { Red: 53, Green: 132, Blue: 228, Alpha: 1, Notation: "rgb" }
      Parse("RGBA(53,132,228,0.5)") => Alpha 0.5, Notation "rgb"
      Parse("rgb(53 132 228 / 50%)") => Alpha 0.5, Notation "rgb"
      Parse("rgb(100%, 0%, 50%)") => { Red: 255, Green: 0, Blue: 128 }
      Parse("hsl(210, 50%, 40%)") => { Red: 51, Green: 102, Blue: 153, Alpha: 1, Notation: "hsl" }
      Parse("hsl(120deg 100% 25%)") => { Red: 0, Green: 128, Blue: 0 }
      Parse("rgb(256, 0, 0)") => nothing; out of range
      Parse("#12345") => nothing; five digits
      Parse("the colour #3584e4") => nothing; only a colour on its own
      Parse("red") => nothing; colour names are not read
      Format({ 53, 132, 228, 1 }, "rgb") => "rgb(53, 132, 228)"
      Format({ 53, 132, 228, 1 }, "hsl") => "hsl(213, 76%, 55%)"
      Format({ 53, 132, 228, 1 }, "hex") => "#3584e4"
      Format({ 53, 132, 228, 0.5 }, "rgb") => "rgba(53, 132, 228, 0.5)"
      Format({ 53, 132, 228, 0.5 }, "hex") => "#3584e480"
      Format({ 255, 0, 0, 0.25 }, "hsl") => "hsla(0, 100%, 50%, 0.25)"
      Format({ 128, 128, 128, 1 }, "hsl") => "hsl(0, 0%, 50%)"
      the actions of text "#3584e4" => "Paste as RGB colour" described "Pastes rgb(53, 132, 228).", and "Paste as HSL colour" described "Pastes hsl(213, 76%, 55%)."
      the actions of text "hsl(210, 50%, 40%)" => "Paste as hex colour" ("Pastes #336699."), and "Paste as RGB colour" ("Pastes rgb(51, 102, 153).")
      ExecuteAsync of Paste as RGB colour on text "#3584e4" => pastes { Text: "rgb(53, 132, 228)" }; the history is unchanged
  how
    decide three actions, one per notation, each left out for a colour already in it
      because the user knows the notation they need; one action that cycles or asks would cost an extra step while holding keys
    decide only a colour on its own, never one inside a longer text
      because which part of a longer text to replace would be a guess
    decide colour names (red, rebeccapurple) are not read
      because almost every English word "red" or "orange" copied would offer three colour actions
    decide the description shows the result
      because the user sees what will be pasted before letting go of Ctrl (#62: descriptions are written for the item)
    decide hex is written in lower case, rgb() and hsl() with commas and a space, and the alpha only below 1
      because that is what CSS and most design tools write, and it pastes into the oldest places that read colours
    decide hsl() values are rounded to whole numbers
      because that is how people write them; converting back may then be off by one in a channel, which no one can see
  uses      ClipboardAction, ClipboardItem, ActionContext, Localization
  sources
    the owner's request in this change: "R6 Convert colour"; GitHub issue #299
```
