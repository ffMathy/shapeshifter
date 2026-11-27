using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Text;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that pastes formatted text as Markdown, keeping its headings, lists, links and emphasis.
/// </summary>
public sealed class PasteAsMarkdownAction : IClipboardAction
{
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-as-markdown";

    /// <summary>
    /// Where the action is placed among the actions.
    /// </summary>
    public int Order => 22;

    /// <summary>
    /// Gets the title shown for the action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdown;
    }

    /// <summary>
    /// Gets the description shown for the action.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdownDescription;
    }

    /// <summary>
    /// Tells whether the item is text with an HTML format whose Markdown holds something other than white space.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the action can be offered for the item.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.Kind != ClipboardContentKind.Text)
        {
            return false;
        }

        var html = FindHtml(item);
        return html is not null && !string.IsNullOrWhiteSpace(Convert(html));
    }

    /// <summary>
    /// Pastes the item's HTML converted to Markdown, without adding it to the history.
    /// </summary>
    /// <param name="item">The item to paste.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the paste.</param>
    /// <returns>A task that completes when the paste is done.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        var markdown = Convert(FindHtml(item) ?? string.Empty);
        return context.WriteAndPasteAsync(new ClipboardSnapshot { Text = markdown }, cancellationToken);
    }

    /// <summary>
    /// Converts HTML to GitHub-flavoured Markdown whose lines end in a line feed, trimmed of white space at its start and end.
    /// </summary>
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
        return new ReverseMarkdown.Converter(config).Convert(html).Trim();
    }

    private static string? FindHtml(ClipboardItem item)
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
