using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// The action that pastes formatted text as Markdown, keeping headings, lists, links and emphasis.
/// </summary>
public sealed class PasteAsMarkdownAction : IClipboardAction
{
    /// <summary>The action's unique id.</summary>
    public string Id => "paste-as-markdown";

    /// <summary>Where the action appears among the actions.</summary>
    public int Order => 22;

    /// <summary>Gets the action's title shown in the actions list.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdown;
    }

    /// <summary>Gets the action's description shown in the actions list.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdownDescription;
    }

    /// <summary>Tells whether the item is text with an HTML format whose Markdown holds something to paste.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        var html = HtmlOf(item);
        return html is not null && !string.IsNullOrWhiteSpace(Convert(html));
    }

    /// <summary>Pastes the item's HTML converted to Markdown, without adding it to the history.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the text has been pasted.</returns>
    public async Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var markdown = Convert(HtmlOf(item) ?? string.Empty);
        await context.WriteAndPasteAsync(new ClipboardSnapshot { Text = markdown }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Converts HTML to GitHub-flavoured Markdown whose lines end in a line feed, trimmed of surrounding white space.</summary>
    /// <param name="html">The HTML to convert.</param>
    /// <returns>The Markdown.</returns>
    public static string Convert(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var config = new ReverseMarkdown.Config { GithubFlavored = true };
        config.Tags.Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass;
        config.Formatting.RemoveComments = true;
        config.Links.SmartHref = true;
        config.Formatting.OutputLineEnding = "\n";
        var markdown = new ReverseMarkdown.Converter(config).Convert(html);
        return markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    }

    private static string? HtmlOf(ClipboardItem item)
    {
        foreach (var format in item.Snapshot.RawFormats)
        {
            if (ClipboardFormatClassifier.IsHtml(format.Name))
            {
                return HtmlText.Decode(format.Data);
            }
        }

        return null;
    }
}
