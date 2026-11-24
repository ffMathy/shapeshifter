using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Shapeshifter.Core.Localization;
using Shapeshifter.Desktop.Services;
using SkiaSharp;
using Path = System.IO.Path;

namespace Shapeshifter.ScreenshotGenerator;

/// <summary>
/// Renders the Microsoft Store screenshots (1920 × 1080) and logos: for every UI language, a headline over the real
/// picker (or settings window) in the Windows look, with the app and the headline in that language. The texts come from
/// <c>packaging/store/listing/text/&lt;language&gt;.json</c>; <c>packaging/store/listing/make-listing.sh</c> runs this
/// (<c>--store &lt;folder&gt; [--languages en,de]</c>) and packs the result for Partner Center. Writes
/// <c>&lt;folder&gt;/&lt;language&gt;/screenshots/0N-&lt;scene&gt;.png</c> and <c>&lt;folder&gt;/logos/*.png</c>.
/// </summary>
/// <remarks>
/// Render on Windows, where Segoe UI and fonts for every script are installed (the Store listing workflow does); elsewhere
/// the Windows look falls back to Inter, and other scripts to whatever fonts the machine has (Noto on Linux).
/// </remarks>
internal static class StoreListing
{
    private const int Width = 1920;
    private const int Height = 1080;

    /// <summary>The screenshots, in the order the Store shows them.</summary>
    private static readonly string[] Scenes = ["history", "actions", "search", "settings", "dark"];

    public static int Run(string[] args)
    {
        var output = Path.GetFullPath(Value(args, "--store") ?? throw new ArgumentException("--store needs a folder"));
        var texts = TextFolder();
        var languages = (Value(args, "--languages")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? ["en", .. Localizer.Translations])
            .Where(language => File.Exists(Path.Combine(texts, language + ".json")))
            .ToList();
        if (languages.Count == 0)
        {
            Console.Error.WriteLine($"No listing texts in {texts}.");
            return 1;
        }

        // The Store copy is paid: the sample text must not call it freeware, as the README's Wikipedia quote does.
        Program.Article = "Shapeshifter is a clipboard manager that keeps everything you copy, so you can paste any of it later, " +
            "originally developed by Flamefusion and later released as open source.";

        foreach (var language in languages)
        {
            var culture = CultureInfo.GetCultureInfo(language);
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture(culture.Name);
            using var text = JsonDocument.Parse(File.ReadAllText(Path.Combine(texts, language + ".json")));
            var shots = text.RootElement.GetProperty("screenshots");
            var folder = Path.Combine(output, language, "screenshots");
            Directory.CreateDirectory(folder);
            for (var i = 0; i < Scenes.Length; i++)
            {
                var path = Path.Combine(folder, $"{i + 1:00}-{Scenes[i]}.png");
                Render(Scenes[i], shots.GetProperty(Scenes[i]), path);
            }

            Console.WriteLine($"{language}: {Scenes.Length} screenshots");
        }

        var logos = Path.Combine(output, "logos");
        Directory.CreateDirectory(logos);
        foreach (var (name, width, height) in new[] { ("poster-1440x2160.png", 1440, 2160), ("boxart-2160x2160.png", 2160, 2160), ("tile-300x300.png", 300, 300) })
        {
            using var bitmap = Logo(width, height, tile: name.StartsWith("tile", StringComparison.Ordinal));
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(logos, name), data.ToArray());
        }

        Console.WriteLine($"Wrote {output}");
        return 0;
    }

    private static string? Value(string[] args, string option)
    {
        var index = Array.IndexOf(args, option);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>packaging/store/listing/text, found by walking up from the working folder or the tool itself.</summary>
    private static string TextFolder()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Shapeshifter.slnx")))
                    return Path.Combine(dir.FullName, "packaging", "store", "listing", "text");
            }
        }

        throw new InvalidOperationException("Shapeshifter.slnx not found");
    }

    private static void Render(string scene, JsonElement shot, string path)
    {
        var dark = scene == "dark";
        // The dark scene shows a chosen accent colour too: purple, one of Windows' own accent choices.
        var application = Application.Current!;
        PlatformLook.Apply(application, Look.Windows, dark ? ThemeVariant.Dark : ThemeVariant.Light);
        AccentPalette.Apply(application, Color.Parse(dark ? "#9A6CF0" : PlatformLook.Resource(application, "LookDefaultAccent", "#0067C0")));

        Window? owner;
        Control ui;
        if (scene == "settings")
        {
            var settings = Program.CreateSettingsWindow(storeCopy: true);
            ui = new Border { Background = settings.Background, CornerRadius = new CornerRadius(8), ClipToBounds = true, Child = Detach(settings) };
            owner = settings;
        }
        else
        {
            var (picker, content) = Program.CreatePickerContent(Look.Windows,
                selectedAction: scene == "actions" ? 2 : null,
                search: scene == "search" ? "shapeshifter" : null);
            ui = content;
            owner = picker;
        }

        var ink = new SolidColorBrush(Color.Parse(dark ? "#FFFFFF" : "#11161F"));
        var muted = new SolidColorBrush(Color.Parse(dark ? "#C9D3E4" : "#3D4656"));
        var heading = new StackPanel
        {
            Margin = new Thickness(120, 62, 120, 0),
            Spacing = 14,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = shot.GetProperty("headline").GetString(), FontSize = 58, FontWeight = FontWeight.Bold, Foreground = ink,
                    TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
                },
                new TextBlock
                {
                    Text = shot.GetProperty("subline").GetString(), FontSize = 26, Foreground = muted, MaxWidth = 1320,
                    TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Center,
                },
            },
        };

        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        grid.Children.Add(heading);
        var box = new Viewbox
        {
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            Margin = new Thickness(120, 36, 120, 44),
            VerticalAlignment = VerticalAlignment.Top,
            Child = new LayoutTransformControl { LayoutTransform = new ScaleTransform(1.3, 1.3), Child = ui },
        };
        Grid.SetRow(box, 1);
        grid.Children.Add(box);

        using var background = Program.ToBitmap(SampleArt.Desktop(Look.Windows, Width, Height, dark));
        var host = new Window
        {
            Width = Width,
            Height = Height,
            WindowDecorations = WindowDecorations.None,
            FontFamily = WithFallback(owner.FontFamily),
            FlowDirection = Localizer.IsRightToLeft ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            Content = new Panel { Children = { new Image { Source = background, Stretch = Stretch.Fill }, grid } },
        };

        Program.Capture(host, path);
        owner.Close();
    }

    /// <summary>
    /// The look's font, followed off Windows by the Noto font for the language's script. Windows' own fonts cover every
    /// script; elsewhere the look's Segoe UI is missing, and the fallback Avalonia picks then draws Arabic and Persian
    /// letters without joining them. The UI variants have the shorter line height the app's rows are made for.
    /// </summary>
    private static FontFamily WithFallback(FontFamily font)
    {
        if (OperatingSystem.IsWindows())
            return font;
        var script = CultureInfo.CurrentUICulture.Name switch
        {
            "ar" or "fa" => "Noto Sans Arabic UI, Noto Sans Arabic",
            "hi" => "Noto Sans Devanagari UI, Noto Sans Devanagari",
            "bn" => "Noto Sans Bengali UI, Noto Sans Bengali",
            "th" => "Noto Sans Thai UI, Noto Sans Thai",
            "ja" => "Noto Sans CJK JP",
            "ko" => "Noto Sans CJK KR",
            "zh-Hans" => "Noto Sans CJK SC",
            _ => "Noto Sans",
        };
        // Arabic and Persian go first: shaped in one font, their letters join; Latin text falls through to the look's font.
        return Localizer.IsRightToLeft
            ? new FontFamily($"DejaVu Sans, {script}, {font}, Inter")
            : new FontFamily($"{font}, Inter, {script}");
    }

    /// <summary>A window's content, moved out of it so it renders inside the screenshot.</summary>
    private static Control Detach(Window window)
    {
        var content = (Control)window.Content!;
        window.Content = null;
        content.DataContext = window.DataContext;
        content.FlowDirection = window.FlowDirection;
        if (!double.IsNaN(window.Width))
            content.Width = window.Width - (content.Margin.Left + content.Margin.Right);
        if (!double.IsNaN(window.Height))
            content.Height = window.Height - (content.Margin.Top + content.Margin.Bottom);
        return content;
    }

    // ------------------------------------------------------------------ logos

    /// <summary>
    /// The Windows look's app icon (assets/icons/windows/application.svg), drawn as vectors at any size. A tile is the
    /// icon alone on a transparent background; poster and box art put it, larger, on a light backdrop with the name.
    /// </summary>
    private static SKBitmap Logo(int width, int height, bool tile)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        float side;
        SKPoint origin;
        if (tile)
        {
            side = Math.Min(width, height) * 1.06f; // the artwork has its own margin inside its 256 px box
            origin = new SKPoint((width - side) / 2, (height - side) / 2);
        }
        else
        {
            using var backdrop = new SKPaint
            {
                Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height),
                    [SKColor.Parse("#EAF2FC"), SKColor.Parse("#B9D4F2")], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(0, 0, width, height, backdrop);
            side = Math.Min(width, height) * 0.62f;
            origin = new SKPoint((width - side) / 2, (height - side) / 2 - Math.Min(width, height) * 0.1f);
        }

        Icon(canvas, origin.X, origin.Y, side);

        if (!tile)
        {
            using var typeface = SKFontManager.Default.MatchFamily("Segoe UI", SKFontStyle.Bold)
                ?? SKFontManager.Default.MatchFamily("Inter", SKFontStyle.Bold) ?? SKTypeface.Default;
            using var font = new SKFont(typeface, Math.Min(width, height) * 0.1f);
            using var ink = new SKPaint { Color = SKColor.Parse("#11161F"), IsAntialias = true };
            // The artwork reaches the bottom of its 256 px box (y 236): the name goes a little below that.
            canvas.DrawText("Shapeshifter", width / 2f, origin.Y + side * 0.96f + font.Size * 1.1f, SKTextAlign.Center, font, ink);
        }

        return bitmap;
    }

    /// <summary>application.svg (a 256 × 256 box), translated to Skia.</summary>
    private static void Icon(SKCanvas canvas, float x, float y, float side)
    {
        canvas.Save();
        canvas.Translate(x, y);
        canvas.Scale(side / 256f);

        static SKShader Gradient(float x0, float y0, float x1, float y1, SKRect box, params (float Offset, string Color, float Opacity)[] stops) =>
            SKShader.CreateLinearGradient(
                new SKPoint(box.Left + box.Width * x0, box.Top + box.Height * y0),
                new SKPoint(box.Left + box.Width * x1, box.Top + box.Height * y1),
                stops.Select(s => SKColor.Parse(s.Color).WithAlpha((byte)(s.Opacity * 255))).ToArray(),
                stops.Select(s => s.Offset).ToArray(),
                SKShaderTileMode.Clamp);

        // The drop shadow of the whole group (feDropShadow dy=8, stdDeviation=8, #0A2A5C at 22 %).
        using var layer = new SKPaint
        {
            ImageFilter = SKImageFilter.CreateDropShadow(0, 8, 8, 8, SKColor.Parse("#0A2A5C").WithAlpha(56)),
        };
        canvas.SaveLayer(layer);

        var back = new SKRect(40, 34, 168, 192);
        canvas.Save();
        canvas.RotateDegrees(-12, 104, 113);
        using (var paint = new SKPaint { IsAntialias = true, Shader = Gradient(0, 0, 1, 1, back, (0, "#FFDC7A", 1), (1, "#F29A12", 1)) })
            canvas.DrawRoundRect(back, 10, 10, paint);
        canvas.Restore();

        var mid = new SKRect(60, 52, 200, 220);
        canvas.Save();
        canvas.RotateDegrees(-5, 130, 136);
        using (var paint = new SKPaint { IsAntialias = true, Shader = Gradient(0, 0, 1, 1, mid, (0, "#7BE8D4", 1), (1, "#0E9A90", 1)) })
            canvas.DrawRoundRect(mid, 32, 32, paint);
        using (var stroke = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 2, Color = SKColors.White.WithAlpha(89) })
            canvas.DrawRoundRect(mid, 32, 32, stroke);
        canvas.Restore();

        using var front = SKPath.ParseSvgPathData("M100 80 H164 A56 56 0 0 1 220 136 V220 A16 16 0 0 1 204 236 H100 A16 16 0 0 1 84 220 V96 A16 16 0 0 1 100 80 Z");
        var box = front.Bounds;
        using (var paint = new SKPaint { IsAntialias = true, Shader = Gradient(0.1f, 0, 0.9f, 1, box, (0, "#6CC0FF", 1), (0.55f, "#1C80E2", 1), (1, "#0A4FB0", 1)) })
            canvas.DrawPath(front, paint);
        using (var shine = new SKPaint { IsAntialias = true, Shader = Gradient(0, 0, 0, 1, box, (0, "#FFFFFF", 0.42f), (0.45f, "#FFFFFF", 0)) })
            canvas.DrawPath(front, shine);

        using (var bar = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha(240) })
        {
            canvas.DrawRoundRect(new SKRect(108, 134, 170, 146), 6, 6, bar);
            canvas.DrawRoundRect(new SKRect(108, 162, 194, 174), 6, 6, bar);
            canvas.DrawRoundRect(new SKRect(108, 190, 158, 202), 6, 6, bar);
        }

        canvas.Restore(); // the shadow layer
        canvas.Restore();
    }
}
