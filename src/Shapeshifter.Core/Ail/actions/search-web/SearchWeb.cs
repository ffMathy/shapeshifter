using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that looks up a copied text with a web search in the user's browser.
/// </summary>
public sealed class SearchWebAction : IClipboardAction
{
    /// <summary>
    /// The name of the search engine the text is sent to.
    /// </summary>
    public const string EngineName = "DuckDuckGo";

    /// <summary>
    /// The longest text, in characters, that is searched.
    /// </summary>
    public const int MaxLength = 200;

    private const string SearchPrefix = "https://duckduckgo.com/?q=";

    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "search-web";

    /// <summary>
    /// The position of this action in the list of actions.
    /// </summary>
    public int Order => 220;

    /// <summary>
    /// Gives the link that searches the web for the text, or null when the text is not one to search.
    /// </summary>
    /// <param name="text">The copied text, or null.</param>
    /// <returns>The absolute search link, or null.</returns>
    public static Uri? SearchUrl(string? text)
    {
        if (text is null)
        {
            return null;
        }

        string trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return null;
        }

        if (trimmed.Contains('\n', StringComparison.Ordinal) || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return null;
        }

        bool hasLetterOrDigit = false;
        bool hasWhiteSpace = false;
        foreach (char c in trimmed)
        {
            if (char.IsLetterOrDigit(c))
            {
                hasLetterOrDigit = true;
            }

            if (char.IsWhiteSpace(c))
            {
                hasWhiteSpace = true;
            }
        }

        if (!hasLetterOrDigit)
        {
            return null;
        }

        bool startsAsLink = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (startsAsLink && !hasWhiteSpace)
        {
            return null;
        }

        return new Uri(SearchPrefix + Uri.EscapeDataString(trimmed), UriKind.Absolute);
    }

    /// <summary>
    /// Gives the title of this action for the item.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionSearchWeb;
    }

    /// <summary>
    /// Gives the description of this action for the item, naming the search engine.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionSearchWebDescription, EngineName);
    }

    /// <summary>
    /// Tells whether the item is a text that can be searched on the web.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is a searchable text.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && SearchUrl(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Opens the web search for the item's text in the browser, and pastes nothing.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context that gives the launcher.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the link has been handed to the launcher.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        Uri? url = SearchUrl(item.Snapshot.Text);
        if (url is not null)
        {
            context.Launcher.OpenUrl(url);
        }

        return Task.CompletedTask;
    }
}
