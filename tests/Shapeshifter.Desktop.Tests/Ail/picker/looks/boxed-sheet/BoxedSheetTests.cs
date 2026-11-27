// ail-derived-from: BoxedSheet@a498c56d1888ec88
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the BoxedSheet look: the picker as a GNOME libadwaita sheet on Linux.</summary>
public sealed class BoxedSheetTests
{
    /// <summary>The Linux picker shows the Boxed Sheet header, boxed lists and the accent-marked choice.</summary>
    [Fact(DisplayName = "the Linux picker")]
    public Task TheLinuxPicker() => AppHarness.RunAsync(async app =>
    {
        for (var i = 1; i <= 6; i++)
        {
            await app.CopyAsync($"other item {i}", null);
        }

        await app.CopyAsync("dotnet run --project src/App", null);

        await app.HoldCtrlVAsync();
        Assert.True(app.PickerVisible);
        Window picker = app.Picker!;

        var texts = AppHarness.Texts(picker).ToList();

        // The header shows "Shapeshifter" centred.
        Assert.Contains("Shapeshifter", texts);
        var title = Visible<TextBlock>(picker).First(t => t.Text == "Shapeshifter");
        var titleBounds = BoundsIn(title, picker);
        Assert.NotNull(titleBounds);
        var windowCentre = picker.Bounds.Width / 2;
        // The icon (20) and its gap (8) sit before the title, so the group is centred, not the title alone.
        Assert.InRange(titleBounds.Value.Center.X, windowCentre - 24, windowCentre + 24);

        // "History" above the history's card, with "7 items".
        Assert.Contains("History", texts);
        Assert.Contains("7 items", texts);
        var historyHeading = Visible<TextBlock>(picker).First(t => t.Text == "History");
        var countCaption = Visible<TextBlock>(picker).First(t => t.Text == "7 items");
        var newestRow = Visible<TextBlock>(picker).First(t => t.Text == "dotnet run --project src/App");
        var headingBounds = BoundsIn(historyHeading, picker)!.Value;
        var countBounds = BoundsIn(countCaption, picker)!.Value;
        var rowBounds = BoundsIn(newestRow, picker)!.Value;
        Assert.True(headingBounds.Bottom <= rowBounds.Top, "\"History\" should be above the history's card");
        Assert.True(countBounds.Left > headingBounds.Right, "\"7 items\" should be at the card's end");

        // The chosen row: tinted pale blue and ringed in blue.
        var borders = Visible<Border>(picker).ToList();
        Assert.Contains(borders, b =>
            b.Background is ISolidColorBrush s && IsBlue(s.Color) && Alpha(s) is > 0.04 and < 0.35);
        Assert.Contains(borders, b =>
            b.BorderBrush is ISolidColorBrush s && IsBlue(s.Color) && Alpha(s) > 0.8
            && Math.Abs(b.BorderThickness.Left - 2) < 0.01);

        // Its tile white with a blue icon.
        var whiteTiles = borders.Where(b =>
            b.Background is ISolidColorBrush s && s.Color.R == 255 && s.Color.G == 255 && s.Color.B == 255 && Alpha(s) > 0.95
            && Math.Abs(b.Bounds.Width - 40) < 0.5 && Math.Abs(b.Bounds.Height - 40) < 0.5).ToList();
        Assert.NotEmpty(whiteTiles);
        Assert.Contains(whiteTiles, tile => ColorsIn(tile).Any(IsBlue));

        // "Actions" and "→ to choose" head the actions' card.
        Assert.Contains("Actions", texts);
        Assert.Contains("→ to choose", texts);

        // "Paste" is blue with a blue check mark at its end.
        var paste = Visible<TextBlock>(picker).First(t => t.Text == "Paste");
        Assert.True(paste.Foreground is ISolidColorBrush ps && IsBlue(ps.Color), "\"Paste\" should be blue");
        var pasteBounds = BoundsIn(paste, picker)!.Value;
        var checkMark = picker.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible && !ReferenceEquals(c, paste))
            .Where(c => c is Shape or PathIcon or TextBlock)
            .Where(c => OwnColors(c).Any(IsBlue))
            .Select(c => BoundsIn(c, picker))
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .Where(r => r.Left >= pasteBounds.Right && r.Top <= pasteBounds.Bottom && r.Bottom >= pasteBounds.Top);
        Assert.NotEmpty(checkMark);

        AppHarness.SaveScreen(picker, "the-linux-picker.png");
    });

    /// <summary>Without a compositor the sheet is opaque: it fills its window with square corners, a #C0BFBC border and no shadow.</summary>
    [Fact(DisplayName = "without a compositor")]
    public Task WithoutACompositor() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("an item", null);
        await app.HoldCtrlVAsync();
        Assert.True(app.PickerVisible);
        Window picker = app.Picker!;

        var expected = Color.Parse("#C0BFBC");
        var sheets = Visible<Border>(picker)
            .Where(b => b.BorderBrush is ISolidColorBrush s && s.Color == expected)
            .ToList();
        Assert.NotEmpty(sheets);

        var sheet = sheets.FirstOrDefault(b =>
            Math.Abs(b.Bounds.Width - picker.ClientSize.Width) < 1
            && Math.Abs(b.Bounds.Height - picker.ClientSize.Height) < 1);
        Assert.NotNull(sheet);
        Assert.Equal(default(CornerRadius), sheet.CornerRadius);
        Assert.Equal(0, sheet.BoxShadow.Count);

        AppHarness.SaveScreen(picker, "without-a-compositor.png");
    });

    private static IEnumerable<T> Visible<T>(Window window) where T : Control =>
        AppHarness.Find<T>(window).Where(c => c.IsEffectivelyVisible);

    private static Rect? BoundsIn(Visual visual, Visual root)
    {
        var matrix = visual.TransformToVisual(root);
        return matrix is Matrix m ? new Rect(visual.Bounds.Size).TransformToAABB(m) : null;
    }

    private static bool IsBlue(Color c) => c.A > 0 && c.B > c.R + 60 && c.B >= c.G;

    private static double Alpha(ISolidColorBrush brush) => brush.Color.A / 255.0 * brush.Opacity;

    private static IEnumerable<Color> ColorsIn(Visual root) =>
        root.GetVisualDescendants().OfType<Control>()
            .Where(c => c.IsEffectivelyVisible)
            .SelectMany(OwnColors);

    private static IEnumerable<Color> OwnColors(Control control)
    {
        var brushes = new List<IBrush?>();
        switch (control)
        {
            case Shape shape:
                brushes.Add(shape.Stroke);
                brushes.Add(shape.Fill);
                break;
            case TemplatedControl templated:
                brushes.Add(templated.Foreground);
                break;
            case TextBlock text:
                brushes.Add(text.Foreground);
                break;
        }

        foreach (var brush in brushes)
        {
            if (brush is ISolidColorBrush solid && Alpha(solid) > 0.5)
            {
                yield return solid.Color;
            }
        }
    }
}
