using Shapeshifter.Core.Actions;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Localization;

namespace Shapeshifter.Core.Ail;

/// <summary>
/// An action that searches the web for a copied text in the user's browser, so a word, a name,
/// an error message or a product number can be looked up in one step.
/// </summary>
public sealed class SearchWebAction : IClipboardAction
{
    /// <summary>The name of the search engine the text is sent to.</summary>
    public const string EngineName = "DuckDuckGo";

    /// <summary>The longest text, in characters after trimming, that is offered for a search.</summary>
    public const int MaxLength = 200;

    private const string SearchPrefix = "https://duckduckgo.com/?q=";

    /// <summary>The action's stable identifier.</summary>
    public string Id => "search-web";

    /// <summary>The action's position among the actions.</summary>
    public int Order => 220;

    /// <summary>Gives the link that searches for the text, or null when the text is not suited to a search.</summary>
    /// <param name="text">The copied text, or null.</param>
    /// <returns>The absolute search link, or null.</returns>
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

        var hasLetterOrDigit = false;
        var hasWhiteSpace = false;
        foreach (var c in trimmed)
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

        var isLink = trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        if (isLink && !hasWhiteSpace)
        {
            return null;
        }

        return new Uri(SearchPrefix + Uri.EscapeDataString(trimmed), UriKind.Absolute);
    }

    /// <summary>Gives the action's title.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized title.</returns>
    public string GetTitle(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Strings.ActionSearchWeb;
    }

    /// <summary>Gives the action's description, which names the search engine the text goes to.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>The localized description.</returns>
    public string GetDescription(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return Localizer.Format(Strings.ActionSearchWebDescription, EngineName);
    }

    /// <summary>Tells whether the item is a text that can be searched for.</summary>
    /// <param name="item">The chosen item.</param>
    /// <returns>True when the item is text that gives a search link.</returns>
    public bool CanExecute(ClipboardItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return item.Kind == ClipboardContentKind.Text && SearchUrl(item.Snapshot.Text) is not null;
    }

    /// <summary>Opens the search for the item's text in the browser, and pastes nothing.</summary>
    /// <param name="item">The chosen item.</param>
    /// <param name="context">The context the action runs in.</param>
    /// <param name="cancellationToken">The token that cancels the action.</param>
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
}
