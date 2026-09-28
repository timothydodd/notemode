using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace NoteMode.UiPreview;

/// <summary>
/// The NoteMode logo (src/NoteMode/Assets/notemode-logo.svg: "NM" in Dracula purple on a rounded
/// dark square) drawn natively at every size the MSIX package and the Store listing need, so the
/// images stay sharp and can be regenerated after a logo change.
/// </summary>
internal static class LogoAssets
{
    private static readonly Color Background = Color.Parse("#282a36");
    private static readonly Color Purple = Color.Parse("#bd93f9");

    public static void Render(string outDir)
    {
        Directory.CreateDirectory(outDir);

        // Taskbar / Start list icons: the logo fills the image (plated and unplated alike).
        foreach (var size in new[] { 16, 24, 32, 48, 256 })
        {
            Save(Logo(size), size, size, outDir, $"Square44x44Logo.targetsize-{size}.png");
            Save(Logo(size), size, size, outDir, $"Square44x44Logo.targetsize-{size}_altform-unplated.png");
        }
        foreach (var scale in new[] { 100, 200 })
        {
            var f = scale / 100.0;
            Save(Logo(44 * f), (int)(44 * f), (int)(44 * f), outDir, $"Square44x44Logo.scale-{scale}.png");
            Save(Logo(50 * f), (int)(50 * f), (int)(50 * f), outDir, $"StoreLogo.scale-{scale}.png");
            // Start tiles: the logo at about 60% with the tile's padding around it.
            Save(Tile(71 * f, 71 * f), (int)(71 * f), (int)(71 * f), outDir, $"Square71x71Logo.scale-{scale}.png");
            Save(Tile(150 * f, 150 * f), (int)(150 * f), (int)(150 * f), outDir, $"Square150x150Logo.scale-{scale}.png");
            Save(Tile(310 * f, 310 * f), (int)(310 * f), (int)(310 * f), outDir, $"Square310x310Logo.scale-{scale}.png");
            Save(Tile(310 * f, 150 * f), (int)(310 * f), (int)(150 * f), outDir, $"Wide310x150Logo.scale-{scale}.png");
        }
        Save(Logo(300), 300, 300, outDir, "store-icon-300x300.png");
    }

    /// <summary>The logo as a control, <paramref name="size"/> pixels square.</summary>
    public static Control Logo(double size)
    {
        var unit = size / 128.0; // the SVG's viewBox
        return new Border
        {
            Width = size,
            Height = size,
            Padding = new Thickness(4 * unit),
            Child = new Border
            {
                Background = new SolidColorBrush(Background),
                BorderBrush = new SolidColorBrush(Purple),
                BorderThickness = new Thickness(Math.Max(1, 4 * unit)),
                CornerRadius = new CornerRadius(20 * unit),
                Child = size < 28
                    ? null // "NM" is unreadable this small; the purple-framed square still reads as the icon.
                    : new TextBlock
                    {
                        Text = "NM",
                        FontFamily = StoreScreenshots.Inter,
                        FontWeight = FontWeight.Bold,
                        FontSize = 56 * unit,
                        Foreground = new SolidColorBrush(Purple),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    }
            }
        };
    }

    private static Control Tile(double width, double height)
    {
        var logo = Math.Min(width, height) * 0.6;
        return new Panel
        {
            Width = width,
            Height = height,
            Children = { new Decorator { Child = Logo(logo), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }
        };
    }

    private static void Save(Control control, int width, int height, string outDir, string file)
    {
        var size = new Size(width, height);
        control.Measure(size);
        control.Arrange(new Rect(size));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height));
        bitmap.Render(control);
        bitmap.Save(Path.Combine(outDir, file), PngBitmapEncoderOptions.Default);
        Console.WriteLine($"  {file}");
    }
}
