using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using NoteMode.Services;
using NoteMode.Themes;
using NoteMode.ViewModels;
using NoteMode.Views;

namespace NoteMode;

public partial class App : Application
{
    private Styles? _draculaTheme;
    private Styles? _lightTheme;
    private SyntaxService? _syntaxService;

    public static App? Instance => Current as App;

    public bool IsLightTheme { get; private set; }

    public SyntaxService? GetSyntaxService() => _syntaxService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Load both themes
        _draculaTheme = new DraculaTheme();
        _lightTheme = new LightTheme();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var stateService = new StateService();
            var cacheService = new CacheService();
            _syntaxService = new SyntaxService();
            var fileChangeService = new FileChangeService();
            var noteService = new NoteService();

            // Load initial theme based on saved preference
            var state = stateService.LoadState();
            ApplyTheme(state.UseLightTheme);

            var viewModel = new MainWindowViewModel(stateService, cacheService, _syntaxService, fileChangeService, noteService);

            // Subscribe to theme changes
            viewModel.ThemeChanged += (s, useLightTheme) => ApplyTheme(useLightTheme);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            // Open files passed via command line arguments
            var filesToOpen = desktop.Args?
                .Where(a => !string.IsNullOrEmpty(a) && File.Exists(a))
                .ToArray() ?? Array.Empty<string>();

            if (filesToOpen.Length > 0)
            {
                // Launched via file association: hide the persisted workspace
                // until the user pins the opened file(s).
                viewModel.EnterEphemeralMode();

                foreach (var arg in filesToOpen)
                {
                    viewModel.OpenFile(arg);
                }
            }
            else
            {
                viewModel.EnsureInitialTab();
            }

            desktop.ShutdownRequested += (s, e) =>
            {
                viewModel.SaveState();
                fileChangeService.Dispose();
            };

            // Single-instance: open files forwarded from later launches in this instance.
            SingleInstanceManager.StartServer(receivedArgs =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    HandleForwardedArgs(desktop, viewModel, receivedArgs)));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void HandleForwardedArgs(
        IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel viewModel, string[] args)
    {
        var filesToOpen = args
            .Where(a => !string.IsNullOrEmpty(a) && File.Exists(a))
            .ToArray();

        foreach (var file in filesToOpen)
        {
            viewModel.OpenFile(file);
        }

        // Bring the existing window to the foreground.
        var window = desktop.MainWindow;
        if (window != null)
        {
            if (window.WindowState == Avalonia.Controls.WindowState.Minimized)
                window.WindowState = Avalonia.Controls.WindowState.Normal;

            window.Show();
            window.Activate();

            // Nudge it above other windows without leaving it pinned.
            window.Topmost = true;
            window.Topmost = false;
        }
    }

    public void ApplyTheme(bool useLightTheme)
    {
        IsLightTheme = useLightTheme;

        // Set the system theme variant for native dialogs
        RequestedThemeVariant = useLightTheme ? ThemeVariant.Light : ThemeVariant.Dark;

        // Remove existing theme
        if (_draculaTheme != null && Styles.Contains(_draculaTheme))
        {
            Styles.Remove(_draculaTheme);
        }
        if (_lightTheme != null && Styles.Contains(_lightTheme))
        {
            Styles.Remove(_lightTheme);
        }

        // Add new theme
        var newTheme = useLightTheme ? _lightTheme : _draculaTheme;
        if (newTheme != null)
        {
            Styles.Add(newTheme);
        }

        // Update syntax highlighting colors
        _syntaxService?.SetLightTheme(useLightTheme);

        // Raise event for other components to react
        ThemeChanged?.Invoke(this, useLightTheme);
    }

    public event EventHandler<bool>? ThemeChanged;
}
