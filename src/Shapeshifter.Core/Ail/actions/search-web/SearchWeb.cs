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
    /// The longest text, in characters, that is offered for a web search.
    /// </summary>
    public const int MaxLength = 200;

    private const string SearchPrefix = "https://duckduckgo.com/?q=";

    /// <summary>
    /// The unique id of this action.
    /// </summary>
    public string Id => "search-web";

    /// <summary>
    /// The position of this action among the actions.
    /// </summary>
    public int Order => 220;

    /// <summary>
    /// Gives the link that searches the web for the text, or null when the text is not suited to a search.
    /// </summary>
    /// <param name="text">The text to search for.</param>
    /// <returns>The search link, or null.</returns>
    public static Uri? SearchUrl(string? text)
    {
        if (text is null)
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return null;
        }

        if (trimmed.Contains('\n', StringComparison.Ordinal) || trimmed.Contains('\r', StringComparison.Ordinal))
        {
            return null;
        }

        if (!trimmed.Any(char.IsLetterOrDigit))
        {
            return null;
        }

        if (IsLinkOnItsOwn(trimmed))
        {
            return null;
        }

        return new Uri(SearchPrefix + Uri.EscapeDataString(trimmed), UriKind.Absolute);
    }

    /// <summary>
    /// Gives the title shown for this action.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionSearchWeb;
    }

    /// <summary>
    /// Gives the description shown for this action, naming the search engine.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionSearchWebDescription, EngineName);
    }

    /// <summary>
    /// Tells whether the item is a text that can be searched on the web.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item can be searched.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && SearchUrl(item.Snapshot.Text) is not null;
    }

    /// <summary>
    /// Opens the web search for the item's text in the browser, without pasting anything.
    /// </summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">Cancels the action.</param>
    /// <returns>A task that completes when the search has been opened.</returns>
    public Task ExecuteAsync(ClipboardItem item, ActionContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);

        var url = SearchUrl(item.Snapshot.Text);
        if (url is not null)
        {
            context.Launcher.OpenUrl(url);
        }

        return Task.CompletedTask;
    }

    private static bool IsLinkOnItsOwn(string text)
    {
        var rest = text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            ? text["http://".Length..]
            : text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? text["https://".Length..]
                : null;

        return rest is not null && !rest.Any(char.IsWhiteSpace);
    }
}
