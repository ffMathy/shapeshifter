// ail-derived-from: Refraction@51acbb29a86868df
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Xunit;

namespace Shapeshifter.Desktop.Tests.Ail;

/// <summary>Derived checks for the Refraction look: the Mac (Tahoe) picker as a sheet of Liquid Glass.</summary>
public sealed class RefractionTests
{
    /// <summary>The header names the history, counts it in a pill and ends with the paste hint; the chosen row is an accent capsule with a chevron.</summary>
    [Fact(DisplayName = "the Mac picker")]
    public Task TheMacPicker() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first", null);
        await app.CopyAsync("second", null);
        await app.CopyAsync("third", null);
        await app.CopyAsync("fourth", null);
        await app.CopyAsync("fifth", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();

        var title = texts.IndexOf("Clipboard");
        var count = texts.IndexOf("5 items");
        var hint = texts.IndexOf("Release ⌘ to paste");
        Assert.True(title >= 0, "the header says \"Clipboard\"");
        Assert.True(count > title, "\"5 items\" follows the title");
        Assert.True(hint > count, "the hint \"Release ⌘ to paste\" is at the header's end");

        var pill = AppHarness.Find<TextBlock>(picker).Where(t => t.Text == "5 items").ToList();
        Assert.NotEmpty(pill);
        Assert.Contains(pill, t => HasRoundAncestor(t));

        var capsules = AppHarness.Find<Border>(picker)
            .Where(b => b.CornerRadius.IsUniform
                        && Math.Abs(b.CornerRadius.TopLeft - 30) < 0.5
                        && b.Background is ISolidColorBrush brush
                        && brush.Color.A == 255
                        && brush.Opacity >= 0.999)
            .ToList();
        Assert.NotEmpty(capsules);

        var chevrons = AppHarness.Find<Control>(picker)
            .Where(c => Math.Abs(c.Bounds.Width - 8) < 0.5 && Math.Abs(c.Bounds.Height - 14) < 0.5)
            .ToList();
        Assert.NotEmpty(chevrons);

        Assert.Contains(capsules, capsule => chevrons.Exists(chevron => IsAtEnd(chevron, capsule, picker)));

        AppHarness.SaveScreen(picker, "the-mac-picker.png");
    }, MacOptions());

    /// <summary>After Space keeps the picker open, the header's hint asks for Enter.</summary>
    [Fact(DisplayName = "kept open")]
    public Task KeptOpen() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first", null);
        await app.CopyAsync("second", null);

        await app.HoldCtrlVAsync();
        await TapAsync(app, "Space");

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var texts = AppHarness.Texts(picker).ToList();
        Assert.Contains("Press Enter to paste", texts);
        Assert.DoesNotContain("Release ⌘ to paste", texts);

        AppHarness.SaveScreen(picker, "kept-open.png");
    }, MacOptions());

    /// <summary>Without a compositor the sheet is drawn square and opaque, filling its window.</summary>
    [Fact(DisplayName = "without the system's material")]
    public Task WithoutTheSystemsMaterial() => AppHarness.RunAsync(async app =>
    {
        await app.CopyAsync("first", null);

        await app.HoldCtrlVAsync();

        Assert.True(app.PickerVisible);
        var picker = app.Picker!;
        var expected = Color.Parse("#F5F5F8");
        var client = picker.ClientSize;

        var sheets = AppHarness.Find<Control>(picker)
            .Where(c => BackgroundOf(c) is ISolidColorBrush brush
                        && brush.Color == expected
                        && brush.Opacity >= 0.999
                        && IsSquare(c)
                        && Math.Abs(c.Bounds.Width - client.Width) < 1
                        && Math.Abs(c.Bounds.Height - client.Height) < 1
                        && c.TranslatePoint(new Point(0, 0), picker) is Point origin
                        && Math.Abs(origin.X) < 1
                        && Math.Abs(origin.Y) < 1)
            .ToList();

        var windowIsSheet = picker.Background is ISolidColorBrush own
                            && own.Color == expected
                            && own.Opacity >= 0.999;

        Assert.True(sheets.Count > 0 || windowIsSheet,
            "a square, opaque #F5F5F8 sheet fills the picker's window");

        AppHarness.SaveScreen(picker, "without-the-system-s-material.png");
    }, MacOptions());

    private static AppOptions MacOptions()
    {
        object boxed = new AppOptions { Modifier = "Cmd" };
        var property = typeof(AppOptions).GetProperty("Look")
                       ?? throw new InvalidOperationException("AppOptions has no Look");
        property.SetValue(boxed, NamedValue(property.PropertyType, "MacOS"));
        return (AppOptions)boxed;
    }

    private static async Task TapAsync<TApp>(TApp app, string key)
        where TApp : notnull
    {
        var method = app.GetType()
                         .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                         .FirstOrDefault(m => m.Name == "TapAsync" && m.GetParameters().Length >= 1)
                     ?? throw new InvalidOperationException("the app has no TapAsync");
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        arguments[0] = NamedValue(parameters[0].ParameterType, key);
        for (var i = 1; i < parameters.Length; i++)
        {
            arguments[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : null;
        }

        var result = method.Invoke(app, arguments);
        if (result is Task task)
        {
            await task;
        }
        else if (result is ValueTask valueTask)
        {
            await valueTask;
        }
    }

    private static object NamedValue(Type type, string name)
    {
        if (type.IsEnum)
        {
            return Enum.Parse(type, name);
        }

        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static;
        return type.GetProperty(name, flags)?.GetValue(null)
               ?? type.GetField(name, flags)?.GetValue(null)
               ?? throw new InvalidOperationException($"{type.Name} has no {name}");
    }

    private static IBrush? BackgroundOf(Control control) => control switch
    {
        Border border => border.Background,
        Panel panel => panel.Background,
        TemplatedControl templated => templated.Background,
        _ => null,
    };

    private static bool IsSquare(Control control) => control switch
    {
        Border border => border.CornerRadius == default,
        TemplatedControl templated => templated.CornerRadius == default,
        _ => true,
    };

    private static bool HasRoundAncestor(Control control)
    {
        for (var parent = control.Parent as Control; parent is not null; parent = parent.Parent as Control)
        {
            if (parent is Border border && border.CornerRadius.TopLeft > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAtEnd(Control chevron, Control capsule, Visual window)
    {
        if (chevron.TranslatePoint(new Point(0, 0), window) is not Point c
            || capsule.TranslatePoint(new Point(0, 0), window) is not Point b)
        {
            return false;
        }

        var inside = c.X >= b.X - 0.5
                     && c.Y >= b.Y - 0.5
                     && c.X + chevron.Bounds.Width <= b.X + capsule.Bounds.Width + 0.5
                     && c.Y + chevron.Bounds.Height <= b.Y + capsule.Bounds.Height + 0.5;
        var rightHalf = c.X > b.X + capsule.Bounds.Width / 2;
        return inside && rightHalf;
    }
}
