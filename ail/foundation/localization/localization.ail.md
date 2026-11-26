# Localization

```ail
node Localization : data   level: system   @existing
  why   Every user-visible text is translated.   <- Foundation
  what
    Strings.<Key>: text   — the text of that key in Strings.resx, in the user's language
    Localizer.Format(format: text, values) -> text   — fills in {0}, {1} … in the user's culture
  sources
    src/Shapeshifter.Core/Localization/Strings.resx, Localizer.cs
```
