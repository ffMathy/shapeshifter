// ail-derived-from: ActionsList@884cab5763c84086
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the ActionsList view: the actions shown to the right of the history.</summary>
public sealed class ActionsListTests
{
    private static readonly Color[] AccentShades =
    [
        Color.Parse("#3584E4"),
        Color.Parse("#1C71D8"),
        Color.Parse("#255C9F"),
    ];

    /// <summary>Scenario: the actions of a text.</summary>
    [Fact(DisplayName = "the actions of a text")]
    public Task TheActionsOfAText() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);

        List<string> texts = [.. AppHarness.Texts(app.Picker!)];
        var start = IndexOfSequence(
            texts,
            "Paste",
            "Pastes clipboard contents as-is.",
            "Paste as file",
            "Saves this text to a .txt file and pastes the file.",
            "Pin to clipboard");
        Assert.True(start >= 0, "The actions should read Paste, Paste as file and Pin to clipboard, each with its description: " + string.Join(" | ", texts));
        var pinDescriptionIndex = start + 5;
        Assert.True(pinDescriptionIndex < texts.Count, "Pin to clipboard should have a description");
        Assert.False(string.IsNullOrWhiteSpace(texts[pinDescriptionIndex]), "Pin to clipboard should have a description");

        var (title, entry) = EntryOf(AppHarness.Find<TextBlock>(app.Picker!), "Paste");
        Assert.True(IsAccent(title.Foreground, opaque: true), "The title of Paste should be in the accent colour");

        var titleX = XIn(title, entry);
        var accentGlyphs = Glyphs(entry).Where(g => GlyphBrushes(g).Any(b => IsAccent(b, opaque: true))).ToList();
        Assert.True(accentGlyphs.Exists(g => XIn(g, entry) < titleX), "The icon of Paste should be in the accent colour");
        Assert.True(accentGlyphs.Exists(g => XIn(g, entry) > titleX), "Paste should be marked as the default with a check mark in the accent colour at its end");

        AppHarness.SaveScreen(app.Picker!, "the-actions-of-a-text.png");
    });

    /// <summary>Scenario: moving into the actions.</summary>
    [Fact(DisplayName = "moving into the actions")]
    public Task MovingIntoTheActions() => AppHarness.RunAsync(async app =>
    {
        await app.CopyImageAsync(64, 48);
        await app.HoldCtrlVAsync();
        await TapAsync(app, "Right");

        Assert.True(app.PickerVisible);

        var (title, entry) = EntryOf(AppHarness.Find<TextBlock>(app.Picker!), "Paste");
        Assert.True(IsRinged(entry), "Paste should be ringed in the accent colour");

        List<Image> images = [.. AppHarness.Find<Image>(app.Picker!)];
        Assert.NotEmpty(images);
        var thumbnail = images[0];
        foreach (var image in images)
        {
            if (image.Bounds.Width < thumbnail.Bounds.Width)
            {
                thumbnail = image;
            }
        }

        var titleAncestors = title.GetVisualAncestors().ToHashSet();
        Visual row = thumbnail;
        while (row.GetVisualParent() is Visual parent && !titleAncestors.Contains(parent))
        {
            row = parent;
        }

        Assert.False(IsRinged(row), "The image's row should no longer be ringed");
        var backgrounds = SelfAndDescendants(row).SelectMany(Boxes).Select(b => b.Background).ToList();
        Assert.False(backgrounds.Exists(b => IsAccent(b, opaque: true)), "The image's row should keep no strong accent fill");
        Assert.True(backgrounds.Exists(IsFaintAccent), "The image's row should keep a faint accent tint");

        AppHarness.SaveScreen(app.Picker!, "moving-into-the-actions.png");
    });

    /// <summary>Scenario: the heading in the Windows look.</summary>
    [Fact(DisplayName = "the heading in the Windows look")]
    public Task TheHeadingInTheWindowsLook() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("dotnet run --project src/App", null);
        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);

        List<string> texts = [.. AppHarness.Texts(app.Picker!)];
        Assert.Contains("Actions for this text", texts);

        var (title, entry) = EntryOf(AppHarness.Find<TextBlock>(app.Picker!), "Paste");
        var titleX = XIn(title, entry);
        var defaults = SelfAndDescendants(entry)
            .OfType<TextBlock>()
            .Where(t => t.Text == "Default" && t.IsEffectivelyVisible)
            .ToList();
        Assert.True(defaults.Exists(d => XIn(d, entry) > titleX), "\"Default\" should be written at the end of Paste");

        AppHarness.SaveScreen(app.Picker!, "the-heading-in-the-windows-look.png");
    }, WithLook("Windows"));

    // AppOptions with its Look set by name, without naming the Look type's namespace.
    private static AppOptions WithLook(string look)
    {
        var options = new AppOptions();
        var property = typeof(AppOptions).GetProperty("Look")
            ?? throw new InvalidOperationException("AppOptions has no Look");
        var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        property.SetValue(options, Enum.Parse(type, look));
        return options;
    }

    // Taps a GestureKey by name, without naming the GestureKey type's namespace.
    private static Task TapAsync(object app, string key)
    {
        var method = app.GetType().GetMethods().Single(m =>
            m.Name == "TapAsync"
            && m.GetParameters().Length == 1
            && m.GetParameters()[0].ParameterType.IsEnum);
        var value = Enum.Parse(method.GetParameters()[0].ParameterType, key);
        return method.Invoke(app, [value]) switch
        {
            Task task => task,
            ValueTask valueTask => valueTask.AsTask(),
            _ => throw new InvalidOperationException("TapAsync returned no task"),
        };
    }

    private static int IndexOfSequence(List<string> texts, params string[] sequence)
    {
        for (var i = 0; i + sequence.Length <= texts.Count; i++)
        {
            var matches = true;
            for (var j = 0; j < sequence.Length; j++)
            {
                if (texts[i + j] != sequence[j])
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return i;
            }
        }

        return -1;
    }

    // The entry of an action: the nearest ancestor of its title that sits among siblings like it, each with an icon.
    private static (TextBlock Title, Visual Entry) EntryOf(IEnumerable<TextBlock> textBlocks, string title)
    {
        foreach (var candidate in textBlocks.Where(t => t.Text == title))
        {
            Visual current = candidate;
            while (current.GetVisualParent() is Visual parent)
            {
                var type = current.GetType();
                var alike = parent.GetVisualChildren().Count(s => s.GetType() == type && ContainsGlyph(s));
                if (alike >= 2)
                {
                    return (candidate, current);
                }

                current = parent;
            }
        }

        Assert.Fail($"No action entry titled \"{title}\" was found");
        throw new InvalidOperationException();
    }

    private static double XIn(Visual visual, Visual relativeTo) =>
        visual.TranslatePoint(default, relativeTo)?.X ?? double.NaN;

    private static IEnumerable<Visual> SelfAndDescendants(Visual visual) => [visual, .. visual.GetVisualDescendants()];

    private static bool IsGlyph(Visual visual) => visual is PathIcon or Shape;

    private static bool ContainsGlyph(Visual visual) => SelfAndDescendants(visual).Any(IsGlyph);

    private static IEnumerable<Visual> Glyphs(Visual scope) =>
        SelfAndDescendants(scope).Where(v => IsGlyph(v) && v.IsEffectivelyVisible);

    private static IEnumerable<IBrush?> GlyphBrushes(Visual visual) => visual switch
    {
        PathIcon icon => [icon.Foreground],
        Shape shape => [shape.Stroke, shape.Fill],
        _ => [],
    };

    private static IEnumerable<(IBrush? Border, Thickness Thickness, IBrush? Background)> Boxes(Visual visual) => visual switch
    {
        Border b => [(b.BorderBrush, b.BorderThickness, b.Background)],
        ContentPresenter c => [(c.BorderBrush, c.BorderThickness, c.Background)],
        TemplatedControl t => [(t.BorderBrush, t.BorderThickness, t.Background)],
        Panel p => [(null, default, p.Background)],
        _ => [],
    };

    private static bool IsRinged(Visual scope) =>
        SelfAndDescendants(scope)
            .Where(v => v.IsEffectivelyVisible)
            .SelectMany(Boxes)
            .Any(b => MaxSide(b.Thickness) >= 2 && IsAccent(b.Border, opaque: true));

    private static double MaxSide(Thickness t) => Math.Max(Math.Max(t.Left, t.Right), Math.Max(t.Top, t.Bottom));

    private static bool HasAccentHue(Color c) =>
        AccentShades.Any(a => a.R == c.R && a.G == c.G && a.B == c.B);

    private static bool IsAccent(IBrush? brush, bool opaque)
    {
        if (brush is not ISolidColorBrush solid || !HasAccentHue(solid.Color))
        {
            return false;
        }

        return !opaque || (solid.Color.A == 255 && solid.Opacity >= 1);
    }

    private static bool IsFaintAccent(IBrush? brush)
    {
        if (brush is not ISolidColorBrush solid || !HasAccentHue(solid.Color))
        {
            return false;
        }

        var alpha = solid.Color.A / 255.0 * solid.Opacity;
        return alpha > 0 && alpha < 0.5;
    }
}
