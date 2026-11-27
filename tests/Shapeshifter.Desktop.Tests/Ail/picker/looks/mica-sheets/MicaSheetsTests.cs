// ail-derived-from: MicaSheets@c3529f4f680c45ee
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Mica Sheets look of the picker on Windows 11.</summary>
public sealed class MicaSheetsTests
{
    /// <summary>Scenario: light.</summary>
    [Fact(DisplayName = "light")]
    public Task Light() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first copied text", null);
        await app.CopyAsync("second copied text", null);
        await app.CopyAsync("third copied text", null);

        await app.HoldCtrlVAsync();
        Assert.True(app.PickerVisible);
        var picker = app.Picker;
        Assert.NotNull(picker);

        // The header says "Clipboard history" and "3 items" at its end.
        var texts = AppHarness.Texts(picker).ToList();
        var titleIndex = texts.IndexOf("Clipboard history");
        var countIndex = texts.IndexOf("3 items");
        Assert.True(titleIndex >= 0, "The header shows \"Clipboard history\".");
        Assert.True(countIndex > titleIndex, "The count follows the title.");

        var title = RectIn(SingleText(picker, "Clipboard history"), picker);
        var count = RectIn(SingleText(picker, "3 items"), picker);
        Assert.True(count.Left > title.Right, "The count is at the end of the header, after the title.");
        Assert.True(Math.Abs(count.Center.Y - title.Center.Y) < 8, "The count is on the header's line.");

        // The chosen row has a light grey fill and a 3 × 16 accent pill at its start edge.
        var row = SingleChosenRow(picker, c => c.R == 0 && c.G == 0 && c.B == 0 && c.A >= 10 && c.A <= 20);
        var pill = SinglePill(picker);
        AssertPillAtStartOf(pill, row, picker);
        var pillColor = FillOf(pill)!.Value;
        Assert.Equal(255, pillColor.A);
        Assert.True(Saturation(pillColor) > 40, "The pill is in the accent colour, not grey.");

        // The actions panel says "Actions for this text", with "Default" at the end of "Paste".
        Assert.Contains("Actions for this text", texts);
        var paste = RectIn(SingleText(picker, "Paste"), picker);
        var @default = RectIn(SingleText(picker, "Default"), picker);
        Assert.True(@default.Left > paste.Right, "\"Default\" is at the end of \"Paste\".");
        Assert.True(Math.Abs(@default.Center.Y - paste.Center.Y) < 8, "\"Default\" is on the row of \"Paste\".");

        AppHarness.SaveScreen(picker, "light.png");
    }, new AppOptions { Look = Named(new AppOptions().Look, "Windows") });

    /// <summary>Scenario: dark.</summary>
    [Fact(DisplayName = "dark")]
    public Task Dark() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("copied text", null);

        await app.HoldCtrlVAsync();
        Assert.True(app.PickerVisible);
        var picker = app.Picker;
        Assert.NotNull(picker);

        // The flyout is #202020 without a compositor.
        var flyout = Color.Parse("#FF202020");
        var surfaces = new List<Color?> { (picker.Background as ISolidColorBrush)?.Color };
        surfaces.AddRange(AppHarness.Find<Control>(picker).Select(FillOf));
        Assert.Contains(surfaces, c => c == flyout);

        // The text is white.
        var title = SingleText(picker, "Clipboard history");
        Assert.Equal(Colors.White, (title.Foreground as ISolidColorBrush)?.Color);

        // The chosen row has a faint white fill.
        var row = SingleChosenRow(picker, c => c.R == 255 && c.G == 255 && c.B == 255 && c.A >= 12 && c.A <= 28);

        // The pill is a lighter shade of the accent.
        var pill = SinglePill(picker);
        AssertPillAtStartOf(pill, row, picker);
        var pillColor = FillOf(pill)!.Value;
        Assert.Equal(255, pillColor.A);
        Assert.True(Saturation(pillColor) > 40, "The pill is in the accent colour, not grey.");
        Assert.True(Lightness(pillColor) > 0.5, "The pill is a lighter shade of the accent.");

        AppHarness.SaveScreen(picker, "dark.png");
    }, new AppOptions { Look = Named(new AppOptions().Look, "Windows"), Dark = true });

    private static T Named<T>(T sample, string name)
    {
        _ = sample;
        var type = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Enum.Parse(type, name);
    }

    private static TextBlock SingleText(Window window, string text) =>
        Assert.Single(AppHarness.Find<TextBlock>(window), t => t.Text == text);

    private static Control SingleChosenRow(Window window, Func<Color, bool> isSelectedFill) =>
        Assert.Single(AppHarness.Find<Control>(window), c =>
            Math.Abs(c.Bounds.Height - 60) <= 1
            && FillOf(c) is { } fill
            && isSelectedFill(fill));

    private static Control SinglePill(Window window) =>
        Assert.Single(AppHarness.Find<Control>(window), c =>
            Math.Abs(c.Bounds.Width - 3) <= 0.5
            && Math.Abs(c.Bounds.Height - 16) <= 0.5
            && FillOf(c) is { A: > 0 });

    private static void AssertPillAtStartOf(Control pill, Control row, Window window)
    {
        var pillRect = RectIn(pill, window);
        var rowRect = RectIn(row, window);
        Assert.True(Math.Abs(pillRect.Left - rowRect.Left) <= 8, "The pill is at the start edge of the chosen row.");
        Assert.True(pillRect.Center.Y >= rowRect.Top && pillRect.Center.Y <= rowRect.Bottom, "The pill is beside the chosen row.");
    }

    private static Rect RectIn(Control control, Window window)
    {
        var origin = control.TranslatePoint(new Point(0, 0), window);
        Assert.True(origin.HasValue, "The control is in the window.");
        return new Rect(origin.Value, control.Bounds.Size);
    }

    private static Color? FillOf(Control control) => control switch
    {
        Border border => (border.Background as ISolidColorBrush)?.Color,
        Panel panel => (panel.Background as ISolidColorBrush)?.Color,
        Shape shape => (shape.Fill as ISolidColorBrush)?.Color,
        TemplatedControl templated => (templated.Background as ISolidColorBrush)?.Color,
        _ => null,
    };

    private static int Saturation(Color color) =>
        Math.Max(color.R, Math.Max(color.G, color.B)) - Math.Min(color.R, Math.Min(color.G, color.B));

    private static double Lightness(Color color) =>
        (Math.Max(color.R, Math.Max(color.G, color.B)) + Math.Min(color.R, Math.Min(color.G, color.B))) / 2.0 / 255.0;
}
