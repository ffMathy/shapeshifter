# Paste as Markdown

"Paste as Markdown" turns formatted text copied from a web page, a document or
an e-mail into Markdown: headings, bold, italics, lists, links, code and
tables keep their meaning as plain characters.

```ail
node PasteAsMarkdown : fn   level: system
  why   Notes apps, chats, issue trackers, wikis and README files take Markdown;
        pasting formatted text there either loses the formatting or brings
        foreign styles along, and rewriting it by hand is slow.   <- Actions
  what
    PasteAsMarkdownAction is an IClipboardAction with Id "paste-as-markdown" and Order 22:
      CanExecute(item) is true when item.Kind is Text, the item has an HTML format, and PasteAsMarkdownAction.Convert of that HTML holds something other than white space
      GetTitle(item) is Strings.ActionPasteAsMarkdown, "Paste as Markdown"
      GetDescription(item) is Strings.ActionPasteAsMarkdownDescription, "Pastes this text as Markdown, keeping headings, lists, links and emphasis."
      ExecuteAsync pastes ClipboardSnapshot { Text: Convert(the HTML) } with context.WriteAndPasteAsync; nothing is added to the history
    the item's HTML is the Data of the first of item.Snapshot.RawFormats whose Name ClipboardFormatClassifier.IsHtml accepts, read with HtmlText.Decode
    PasteAsMarkdownAction.Convert(html: text) -> text
      converts with ReverseMarkdown (the ReverseMarkdown package, which Shapeshifter.Core references), exactly as: a ReverseMarkdown.Config with GithubFlavored true, then config.Tags.Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass, config.Formatting.RemoveComments = true and config.Links.SmartHref = true; then new ReverseMarkdown.Converter(config).Convert(html)
      the result without the white space at its start and end
    examples
      Convert("<h1>Title</h1><p>Some <b>bold</b> and <i>italic</i> and a <a href=\"https://example.com\">link</a>.</p>") => "# Title\n\nSome **bold** and *italic* and a [link](https://example.com)."
      Convert("<ul><li>one</li><li>two</li></ul>") => "- one\n- two"
      Convert("<ol><li>a</li><li>b</li></ol>") => "1. a\n2. b"
      Convert("<table><tr><th>A</th><th>B</th></tr><tr><td>1</td><td>2</td></tr></table>") => "| A | B |\n| --- | --- |\n| 1 | 2 |"
      Convert("<html><body><!--StartFragment--><p>Hi <code>code</code></p><!--EndFragment--></body></html>") => "Hi `code`"; the comments browsers add are dropped
      Convert("<p>Hi <a href=\"https://e.com\">https://e.com</a> <blink>odd</blink></p>") => "Hi https://e.com odd"; a link whose text is its address stays plain, unknown tags keep only their text
      CanExecute(text "Hello World" + raw "text/html" "<b>Hello</b> World") => true
      CanExecute(text "hello world") => false; no HTML format
      CanExecute(raw "text/html" "<p> </p>" only) => false; nothing to paste
      CanExecute(PNG image) => false
      ExecuteAsync(text "Hello World" + raw "text/html" "<b>Hello</b> World") => pastes { Text: "**Hello** World" }; the history is unchanged
  how
    decide ReverseMarkdown, GitHub-flavoured
      because it is the most used HTML-to-Markdown converter for .NET, MIT licensed, and GitHub's flavour adds tables and fenced code, which most Markdown apps read
    decide unknown tags keep their text, and comments are dropped
      because copied HTML is full of tags and markers that mean nothing in Markdown; their words matter, the markup does not
    decide offered only for text with an HTML format
      because without HTML there is no formatting to keep, and the result would be the plain text, which Paste already gives
    decide what is pasted is not added to the history
      because the other paste actions do the same: the user did not copy it
  uses      ClipboardAction, ClipboardItem, CopiedHtml, ActionContext, Localization
  sources
    the owner's request in this change: "T1 Paste as Markdown"
    ReverseMarkdown: https://github.com/mysticmind/reversemarkdown-net, package ReverseMarkdown 6.3.0
```
