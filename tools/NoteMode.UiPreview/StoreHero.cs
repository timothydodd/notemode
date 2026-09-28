using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace NoteMode.UiPreview;

/// <summary>
/// Store "hero" art and README banner: a 16:9 gradient with the logo, wordmark and tagline on the
/// left and the real NoteMode window floating on the right. Laid out on a 1920x1080 design and
/// rendered natively at each size, so text stays crisp at 4K.
/// </summary>
internal static class StoreHero
{
    private static readonly Color From = Color.Parse("#6D4AFF");
    private static readonly Color To = Color.Parse("#BD4FD8");

    public static void Render(string outDir, string home)
    {
        Directory.CreateDirectory(outDir);

        var ws = SampleWorkspace.Create(Path.Combine(home, "hero"));
        ws.ViewModel.WindowWidth = 1280;
        ws.ViewModel.WindowHeight = 800;
        ws.ViewModel.IsNotesPanelOpen = true;
        var window = Program.OpenMainWindow(ws, light: false);
        using var shot = Program.CaptureFrame(window);
        window.Close();

        RenderSize(outDir, shot, 1920, 1080);
        RenderSize(outDir, shot, 3840, 2160);
    }

    private static void RenderSize(string outDir, Bitmap appWindow, int width, int height)
    {
        var s = width / 1920.0;

        var root = new Panel { Width = width, Height = height, Background = StoreScreenshots.Gradient(From, To) };

        root.Children.Add(new Ellipse
        {
            Width = 1500 * s,
            Height = 1500 * s,
            Fill = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.FromArgb(0x40, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) }
            },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -300 * s, 0)
        });

        // The app window on the right, bleeding slightly off the edge.
        var shotW = 1080 * s;
        var shotH = shotW * appWindow.PixelSize.Height / appWindow.PixelSize.Width;
        root.Children.Add(new Border
        {
            Width = shotW,
            Height = shotH,
            CornerRadius = new CornerRadius(14 * s),
            BoxShadow = BoxShadows.Parse($"0 {40 * s} {90 * s} 0 #66000000, 0 {12 * s} {28 * s} 0 #40000000"),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, -140 * s, 0),
            Child = new Border
            {
                CornerRadius = new CornerRadius(14 * s),
                ClipToBounds = true,
                Child = new Image { Source = appWindow, Stretch = Stretch.UniformToFill }
            }
        });

        var left = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(120 * s, 0, 0, 0),
            MaxWidth = 760 * s
        };
        var logo = LogoAssets.Logo(112 * s);
        logo.HorizontalAlignment = HorizontalAlignment.Left;
        logo.Margin = new Thickness(0, 0, 0, 28 * s);
        left.Children.Add(logo);
        left.Children.Add(Text("NoteMode", 108 * s, FontWeight.Bold, Brushes.White));
        left.Children.Add(Text("The notepad that\nremembers everything.", 46 * s, FontWeight.SemiBold,
            new SolidColorBrush(Colors.White, 0.92), lineHeight: 58 * s, top: 18 * s));
        left.Children.Add(Text("Tabs, unsaved edits and notes survive every restart.\nFast, lightweight, free and open source.",
            28 * s, FontWeight.Normal, new SolidColorBrush(Colors.White, 0.8), lineHeight: 40 * s, top: 26 * s));
        root.Children.Add(left);

        StoreScreenshots.RenderToFile(root, width, height, Path.Combine(outDir, $"store-hero-{width}x{height}.png"));
    }

    private static TextBlock Text(string text, double size, FontWeight weight, IBrush brush, double lineHeight = 0, double top = 0) => new()
    {
        Text = text,
        FontFamily = StoreScreenshots.Inter,
        FontSize = size,
        FontWeight = weight,
        Foreground = brush,
        LineHeight = lineHeight > 0 ? lineHeight : double.NaN,
        Margin = new Thickness(0, top, 0, 0)
    };
}
