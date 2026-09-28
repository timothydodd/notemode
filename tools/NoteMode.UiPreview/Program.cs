using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NoteMode.ViewModels;
using NoteMode.Views;

namespace NoteMode.UiPreview;

/// <summary>
/// Renders NoteMode headlessly and saves PNGs.
/// <list type="bullet">
/// <item><c>NoteMode.UiPreview [outDir]</c>: every window and dialog, dark and light, for checking UI changes.</item>
/// <item><c>--store &lt;outDir&gt;</c>: the 1920x1080 marketing screenshots (README and Store listing).</item>
/// <item><c>--hero &lt;outDir&gt;</c>: the Store hero banner at 1920x1080 and 3840x2160.</item>
/// <item><c>--assets &lt;outDir&gt;</c>: the MSIX logo images (packaging/Assets) and the 300x300 Store icon.</item>
/// </list>
/// </summary>
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var mode = args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal) ? args[0] : "--preview";
        if (mode != "--preview")
            args = args.Skip(1).ToArray();
        var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "ui-preview");
        Directory.CreateDirectory(outDir);

        // Before any NoteMode service runs: keep the sample session out of the real ~/.notemode.
        var home = Path.Combine(Path.GetTempPath(), "notemode-uipreview-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(home);
        Environment.SetEnvironmentVariable("NOTEMODE_HOME", Path.Combine(home, ".notemode"));

        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia()
            .WithInterFont()
            .SetupWithoutStarting();

        try
        {
            switch (mode)
            {
                case "--store":
                    StoreScreenshots.Render(outDir, home);
                    break;
                case "--hero":
                    StoreHero.Render(outDir, home);
                    break;
                case "--assets":
                    LogoAssets.Render(outDir);
                    break;
                default:
                    RenderPreviews(outDir, home);
                    break;
            }
        }
        finally
        {
            try { Directory.Delete(home, recursive: true); } catch { /* temp */ }
        }

        Console.WriteLine($"Written to {outDir}");
        return 0;
    }

    private static void RenderPreviews(string outDir, string home)
    {
        foreach (var light in new[] { false, true })
        {
            var suffix = light ? "-light" : "";
            var ws = SampleWorkspace.Create(Path.Combine(home, light ? "light" : "dark"));
            var window = OpenMainWindow(ws, light);

            Capture(window, Path.Combine(outDir, $"main{suffix}.png"));

            ws.ViewModel.IsExplorerPanelOpen = true;
            Capture(window, Path.Combine(outDir, $"main-explorer{suffix}.png"));

            ws.ViewModel.IsNotesPanelOpen = true;
            ws.ViewModel.SelectedTab = ws.MeetingNote;
            Capture(window, Path.Combine(outDir, $"main-notes{suffix}.png"));

            ws.ViewModel.IsNotesPanelOpen = false;
            ws.ViewModel.IsExplorerPanelOpen = false;
            ShowSearch(window, ws, "order");
            Capture(window, Path.Combine(outDir, $"main-search{suffix}.png"));

            ws.ViewModel.IsSearchPanelOpen = false;
            ws.ViewModel.SelectedTab = ws.ReadmeTab;
            ws.ReadmeTab.IsPreview = true;
            Capture(window, Path.Combine(outDir, $"main-markdown-preview{suffix}.png"));
            ws.ReadmeTab.IsPreview = false;

            ws.ViewModel.IsLoading = true;
            ws.ViewModel.LoadingText = "Loading server.log…";
            Capture(window, Path.Combine(outDir, $"main-loading{suffix}.png"));
            ws.ViewModel.IsLoading = false;

            CaptureDialog(new UnsavedChangesDialog("OrderService.cs"), outDir, $"dialog-unsaved{suffix}.png");
            CaptureDialog(new FileChangedDialog("OrderService.cs"), outDir, $"dialog-file-changed{suffix}.png");
            CaptureDialog(new RenameDialog("Standup notes"), outDir, $"dialog-rename{suffix}.png");
            CaptureDialog(new MessageDialog("Can't save file", "OrderService.cs could not be saved. Your changes are still in NoteMode.\n\nAccess to the path is denied."), outDir, $"dialog-message{suffix}.png");
            CaptureDialog(new LanguagePickerDialog(ws.Syntax, "C#"), outDir, $"dialog-language{suffix}.png");
            CaptureDialog(new SettingsDialog(), outDir, $"dialog-settings{suffix}.png");
            var find = new FindReplaceDialog(GetEditor(window)!);
            CaptureDialog(find, outDir, $"dialog-find{suffix}.png");

            window.Close();
        }
    }

    /// <summary>Shows the main window on the sample workspace in the given theme.</summary>
    public static MainWindow OpenMainWindow(SampleWorkspace ws, bool light)
    {
        ws.Syntax.SetLightTheme(light);
        App.Instance!.ApplyTheme(light);
        ws.ViewModel.UseLightTheme = light;
        foreach (var tab in ws.ViewModel.Tabs)
            tab.RefreshSyntaxHighlighting();

        var window = new MainWindow { DataContext = ws.ViewModel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Opens the search panel and runs a search across the open tabs.</summary>
    public static void ShowSearch(MainWindow window, SampleWorkspace ws, string text)
    {
        ws.ViewModel.IsSearchPanelOpen = true;
        Dispatcher.UIThread.RunJobs();
        var panel = window.GetVisualDescendants().OfType<SearchPanel>().First();
        var search = (SearchPanelViewModel)panel.DataContext!;
        search.IsTabsMode = true;
        search.SearchText = text;
        search.PerformSearch();
        Dispatcher.UIThread.RunJobs();
    }

    public static AvaloniaEdit.TextEditor? GetEditor(Window window) =>
        window.GetVisualDescendants().OfType<EditorView>().FirstOrDefault()?.GetEditor();

    private static void CaptureDialog(Window dialog, string outDir, string file)
    {
        dialog.Show();
        Capture(dialog, Path.Combine(outDir, file));
        dialog.Close();
    }

    public static Bitmap CaptureFrame(Window window)
    {
        // Several passes: the editor lays out, then the tab strip and panels react to it.
        for (var i = 0; i < 3; i++)
            Dispatcher.UIThread.RunJobs();
        return window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
    }

    public static void Capture(Window window, string path)
    {
        using var frame = CaptureFrame(window);
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Console.WriteLine($"  {Path.GetFileName(path)}");
    }
}
