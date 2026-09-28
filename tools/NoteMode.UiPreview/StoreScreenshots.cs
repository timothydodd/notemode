using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NoteMode.Views;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace NoteMode.UiPreview;

/// <summary>
/// Marketing screenshots (README and Microsoft Store listing): 1920x1080 frames, each with a
/// gradient backdrop, a headline and the real NoteMode window floating below it. The window is
/// rendered by the app itself, 1:1 so text stays crisp; only the frame around it is composed here.
/// </summary>
internal static class StoreScreenshots
{
    private const int Width = 1920;
    private const int Height = 1080;

    // Main window size inside the frame.
    private const int WindowWidth = 1440;
    private const int WindowHeight = 790;

    internal static readonly FontFamily Inter = new("fonts:Inter#Inter");

    private sealed record Slide(string File, string Title, string Subtitle, Color From, Color To, bool Light, Action<MainWindow, SampleWorkspace> Setup);

    // Dracula palette: purple #bd93f9, pink #ff79c6, cyan #8be9fd, green #50fa7b, background #282a36.
    private static readonly Slide[] Slides =
    {
        new("01-session", "Close it. Your tabs are still there.",
            "NoteMode remembers every tab, including unsaved edits, and picks up exactly where you left off.",
            Color.Parse("#6D4AFF"), Color.Parse("#BD4FD8"), Light: false,
            (_, _) => { }),
        new("02-notes", "Notes that don't need a file",
            "Jot something down, file it in a folder, find it again next week. No Save dialog.",
            Color.Parse("#B8327A"), Color.Parse("#E46A5A"), Light: false,
            (_, ws) =>
            {
                ws.ViewModel.IsNotesPanelOpen = true;
                ws.ViewModel.SelectedTab = ws.MeetingNote;
            }),
        new("03-search", "Search every tab, or a whole folder",
            "Results with context, one click to jump to the line.",
            Color.Parse("#0E7C86"), Color.Parse("#2F5FD0"), Light: false,
            (window, ws) => Program.ShowSearch(window, ws, "order")),
        new("04-markdown", "Markdown, rendered",
            "Flip any .md file or note into a formatted preview with Ctrl+Shift+V.",
            Color.Parse("#3B4BC8"), Color.Parse("#7A3FD6"), Light: true,
            (_, ws) =>
            {
                ws.ViewModel.SelectedTab = ws.ReadmeTab;
                ws.ReadmeTab.IsPreview = true;
            }),
        new("05-explorer", "Light or dark, with the files beside you",
            "Dracula and a clean light theme. The explorer follows whatever file you are in.",
            Color.Parse("#E7E9F2"), Color.Parse("#C9CEE3"), Light: true,
            (_, ws) =>
            {
                // A file at the project root, so the explorer shows the whole project.
                ws.ViewModel.SelectedTab = ws.ViewModel.Tabs.First(t => t.Title == "appsettings.json");
                ws.ViewModel.IsExplorerPanelOpen = true;
            }),
    };

    public static void Render(string outDir, string home)
    {
        Directory.CreateDirectory(outDir);
        foreach (var slide in Slides)
        {
            using var frame = CaptureWindow(slide, home);
            Compose(frame, slide, Path.Combine(outDir, slide.File + ".png"));
        }
    }

    private static Bitmap CaptureWindow(Slide slide, string home)
    {
        var ws = SampleWorkspace.Create(Path.Combine(home, slide.File));
        ws.ViewModel.WindowWidth = WindowWidth;
        ws.ViewModel.WindowHeight = WindowHeight;
        var window = Program.OpenMainWindow(ws, slide.Light);
        slide.Setup(window, ws);
        var frame = Program.CaptureFrame(window);
        window.Close();
        return frame;
    }

    /// <summary>Lays out headline, subtitle and the floating window on a gradient and renders the frame.</summary>
    private static void Compose(Bitmap frame, Slide slide, string path)
    {
        // Dark text on the pale backdrop, white on the saturated ones.
        var paleBackdrop = slide.From.R + slide.From.G + slide.From.B > 600;
        IBrush titleBrush = paleBackdrop ? new SolidColorBrush(Color.Parse("#282a36")) : Brushes.White;
        var subtitleBrush = paleBackdrop
            ? new SolidColorBrush(Color.Parse("#282a36"), 0.75)
            : new SolidColorBrush(Colors.White, 0.85);

        var header = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 60, 0, 0),
            Spacing = 10
        };
        header.Children.Add(new TextBlock
        {
            Text = slide.Title,
            FontFamily = Inter,
            FontSize = 56,
            FontWeight = FontWeight.Bold,
            Foreground = titleBrush,
            TextAlignment = TextAlignment.Center
        });
        header.Children.Add(new TextBlock
        {
            Text = slide.Subtitle,
            FontFamily = Inter,
            FontSize = 24,
            Foreground = subtitleBrush,
            TextAlignment = TextAlignment.Center
        });

        // The window: 1:1 pixels, rounded corners, soft shadow. Two layers: the outer carries the
        // shadow (a clipped element would cut its own shadow off), the inner clips to the corners.
        var windowCard = new Border
        {
            Width = frame.PixelSize.Width,
            Height = frame.PixelSize.Height,
            CornerRadius = new CornerRadius(10),
            BoxShadow = BoxShadows.Parse("0 40 90 0 #80000000, 0 12 28 0 #40000000"),
            Background = Brushes.Black,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Child = new Border
            {
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Child = new Image { Source = frame, Stretch = Stretch.None }
            }
        };
        // Centre the window in the space below the headline (which ends around y=200).
        const int headerHeight = 200;
        windowCard.Margin = new Thickness(0, 0, 0, Math.Max(40, (Height - headerHeight - frame.PixelSize.Height) / 2.0 + 10));

        var root = new Panel
        {
            Width = Width,
            Height = Height,
            Background = Gradient(slide.From, slide.To)
        };
        // Subtle glow behind the window so the gradient does not look flat.
        root.Children.Add(new Ellipse
        {
            Width = 1400,
            Height = 700,
            Fill = new RadialGradientBrush
            {
                GradientStops = { new GradientStop(Color.FromArgb(0x40, 255, 255, 255), 0), new GradientStop(Color.FromArgb(0, 255, 255, 255), 1) }
            },
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, 0, -200)
        });
        root.Children.Add(windowCard);
        root.Children.Add(header);

        RenderToFile(root, Width, Height, path);
    }

    internal static LinearGradientBrush Gradient(Color from, Color to) => new()
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(from, 0), new GradientStop(to, 1) }
    };

    internal static void RenderToFile(Control content, int width, int height, string path)
    {
        var canvas = new Window
        {
            Width = width,
            Height = height,
            WindowDecorations = WindowDecorations.None,
            Content = content
        };
        canvas.Show();
        Dispatcher.UIThread.RunJobs();
        using var rendered = canvas.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
        rendered.Save(path, PngBitmapEncoderOptions.Default);
        canvas.Close();
        Console.WriteLine($"  {Path.GetFileName(path)}");
    }
}
