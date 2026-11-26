using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes formatted text as Markdown. It keeps headings, lists, links and emphasis
/// for the notes apps, chats, issue trackers, wikis and README files that read Markdown.
/// </summary>
public sealed class PasteAsMarkdownAction : IClipboardAction
{
    /// <summary>The stable identifier of this action.</summary>
    public string Id => "paste-as-markdown";

    /// <summary>The position of this action among the actions.</summary>
    public int Order => 22;

    /// <summary>Gets the title shown for this action.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdown;
    }

    /// <summary>Gets the description shown for this action.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdownDescription;
    }

    /// <summary>
    /// Tells whether the item is text with an HTML format whose Markdown holds something other than white space.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns><see langword="true"/> when this action can paste the item as Markdown.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        string? html = FindHtml(item);
        return html is not null && !string.IsNullOrWhiteSpace(Convert(html));
    }

    /// <summary>
    /// Pastes the item's HTML converted to Markdown, without adding it to the history.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the Markdown has been pasted.</returns>
    public System.Threading.Tasks.Task ExecuteAsync(
        ClipboardItem item,
        ActionContext context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.ArgumentNullException.ThrowIfNull(item);
        System.ArgumentNullException.ThrowIfNull(context);
        string markdown = Convert(FindHtml(item) ?? string.Empty);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = markdown }, cancellationToken);
    }

    /// <summary>
    /// Converts HTML to GitHub-flavoured Markdown, keeping the text of unknown tags and dropping comments.
    /// </summary>
    /// <param name="html">The HTML to convert.</param>
    /// <returns>The Markdown, without white space at its start and end.</returns>
    public static string Convert(string html)
    {
        System.ArgumentNullException.ThrowIfNull(html);
        var config = new ReverseMarkdown.Config { GithubFlavored = true };
        config.Tags.Unknown = ReverseMarkdown.Config.UnknownTagsOption.Bypass;
        config.Formatting.RemoveComments = true;
        config.Links.SmartHref = true;
        return new ReverseMarkdown.Converter(config).Convert(html).Trim();
    }

    private static string? FindHtml(ClipboardItem item)
    {
        foreach (RawClipboardFormat format in item.Snapshot.RawFormats)
        {
            if (ClipboardFormatClassifier.IsHtml(format.Name))
            {
                return HtmlText.Decode(format.Data);
            }
        }

        return null;
    }
}
