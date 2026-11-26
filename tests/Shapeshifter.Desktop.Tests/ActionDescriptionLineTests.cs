using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Serilog.Core;
using Shapeshifter.Core.Clipboard;
using Shapeshifter.Core.Engine;
using Shapeshifter.Core.Input;
using Shapeshifter.Core.Localization;
using Shapeshifter.Core.Platform;
using Shapeshifter.Desktop.Services;

namespace Shapeshifter.Desktop.Tests;

/// <summary>
/// Every action's description must fit on at most 2 lines in the actions pane of every look, in English and in every
/// translation (ail/picker/actions-list). The picker is laid out with real text shaping (Skia and the bundled Inter
/// font), and each description is measured without the look's line limit, so a text that would be cut off fails too.
/// </summary>
public sealed class ActionDescriptionLineTests
{
    private const int MaxLines = 2;

    /// <summary>The descriptions of every action, filled in the way the actions fill them in.</summary>
    private static readonly (string Key, Func<string, string> Fill)[] Descriptions =
    [
        ("ActionPasteDescription", text => text),
        ("ActionPasteAsFileDescription", text => text),
        ("ActionPasteAsImageFileDescription", text => Localizer.Format(text, "jpg")),
        ("ActionPasteAsPlainTextDescription", text => text),
        ("ActionUploadImageDescription", text => Localizer.Format(text, "Catbox")),
        ("ActionUploadTextDescription", text => Localizer.Format(text, "Catbox")),
        ("ActionPasteAsZipDescription", text => text),
        ("ActionCopyImageFromLinkDescription", text => text),
        ("ActionOpenLinkDescription", text => text),
        ("ActionOpenLinksDescription", text => Localizer.Format(text, 10)),
        ("ActionPinDescription", text => text),
        ("ActionUnpinDescription", text => text),
    ];

    public static TheoryData<Look> Looks => [Look.Linux, Look.Windows, Look.MacOS];

    [Theory]
    [MemberData(nameof(Looks))]
    public async Task Every_description_fits_on_two_lines(Look look)
    {
        var tooLong = new List<string>();
        await TestApp.RunAsync(() =>
        {
            PlatformLook.Apply(Application.Current!, look);
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                foreach (var language in new[] { "en" }.Concat(Localizer.Translations))
                {
                    var culture = CultureInfo.GetCultureInfo(language);
                    CultureInfo.CurrentUICulture = culture;
                    CultureInfo.CurrentCulture = culture;
                    foreach (var (key, lines, text) in Measure())
                    {
                        if (lines > MaxLines)
                        {
                            tooLong.Add($"{look} {language} {key}: {lines} lines: {text}");
                        }
                    }
                }
            }
            finally
            {
                CultureInfo.CurrentUICulture = previous;
                CultureInfo.CurrentCulture = previous;
                PlatformLook.Apply(Application.Current!);
            }
        });

        Assert.True(tooLong.Count == 0, string.Join(Environment.NewLine, tooLong));
    }

    // A few at a time, so that the list has room to lay out every entry. Paste always comes first, as the default, the only
    // action that is ever marked as such (which narrows its description in some looks).
    private static IEnumerable<(string Key, int Lines, string Text)> Measure() =>
        Descriptions.Skip(1).Chunk(3)
            .SelectMany((chunk, index) => MeasureSome([Descriptions[0], .. chunk]).Skip(index == 0 ? 0 : 1));

    private static List<(string Key, int Lines, string Text)> MeasureSome((string Key, Func<string, string> Fill)[] descriptions)
    {
        var actions = descriptions
            .Select(description => new PickerActionView(
                description.Key,
                "Title",
                description.Fill(Strings.ResourceManager.GetString(description.Key, CultureInfo.CurrentUICulture)!)))
            .ToArray();
        var presenter = new PickerPresenter(new NoCursor(), new PickerVisuals(new NoFileIcons(), Logger.None), "Ctrl", Logger.None);
        try
        {
            Run(presenter.ShowAsync(new PickerViewState(
                [new ClipboardItem(new ClipboardSnapshot { Text = "text" })], 0, actions, 0, PickerPane.Items)));
            var window = (Window)typeof(PickerPresenter).GetField("window", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(presenter)!;
            var blocks = window.GetVisualDescendants().OfType<TextBlock>()
                .Where(block => block.Classes.Contains("actionDescription"))
                .ToArray();
            Assert.Equal(actions.Length, blocks.Length);

            // Without the look's own limit, so that a description it would cut off is counted in full.
            foreach (var block in blocks)
            {
                block.MaxLines = 0;
            }

            window.UpdateLayout();
            return blocks
                .Select((block, index) => (actions[index].Id, block.TextLayout.TextLines.Count, block.Text ?? string.Empty))
                .ToList();
        }
        finally
        {
            Run(presenter.HideAsync());
        }
    }

    private static void Run(Task task)
    {
        Dispatcher.UIThread.RunJobs();
        Assert.True(task.IsCompletedSuccessfully);
    }

    private sealed class NoFileIcons : IFileIconProvider
    {
        public byte[]? GetIconPng(string path, int size) => null;
    }

    private sealed class NoCursor : ICursorLocator
    {
        public (int X, int Y)? GetCursorPosition() => null;
    }
}
