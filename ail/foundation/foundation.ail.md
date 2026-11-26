# Foundation

Hand-written code that generated code builds on. These nodes are `@existing`:
`ail build` never compiles them, and the nodes that `use` them see their
contracts. Each states only what generated code needs; the code itself, named
under `sources`, holds the rest.

More nodes in this folder, each in a file of its own: [`ClipboardItem`](clipboard-item.ail.md), [`ImageFormat`](image-format.ail.md), [`ClipboardAction`](clipboard-action.ail.md), [`ActionContext`](action-context.ail.md), [`Localization`](localization.ail.md), [`CopiedHtml`](copied-html.ail.md).

```ail
node Foundation : library   level: system
  why   Generated code needs the clipboard's types, the actions' interface and
        the translated text, which stay hand-written while the app moves to
        generated code one node at a time.   <- Shapeshifter
  children  ClipboardItem, ImageFormat, ClipboardAction, ActionContext, Localization, CopiedHtml
```
