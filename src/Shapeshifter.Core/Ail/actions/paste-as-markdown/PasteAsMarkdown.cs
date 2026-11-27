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
    /// <summary>
    /// The action's unique id.
    /// </summary>
    public string Id => "paste-as-markdown";

    /// <summary>
    /// Where the action stands among the actions.
    /// </summary>
    public int Order => 22;

    /// <summary>
    /// The action's title, as shown in the actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdown;
    }

    /// <summary>
    /// The action's description, as shown in the actions.
    /// </summary>
    /// <param name="item">The item the action is for.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionPasteAsMarkdownDescription;
    }

    /// <summary>
    /// Whether the action is offered for an item: text with an HTML format whose Markdown holds something other than white space.
    /// </summary>
    /// <param name="item">The item to check.</param>
    /// <returns>True when the item can be pasted as Markdown.</returns>
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
        var html = FindHtml(item);
        if (html is null)
        {
            return Task.CompletedTask;
        }

        var snapshot = new ClipboardSnapshot { Text = Convert(html) };
        return context.WriteAndPasteAsync(snapshot, cancellationToken);
    }

    /// <summary>
    /// Converts HTML to GitHub-flavoured Markdown whose lines end in a line feed, without white space at its start and end.
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
