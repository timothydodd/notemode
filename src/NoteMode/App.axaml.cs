using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Platform.Storage;
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
    private Styles? _sharedStyles;
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
        _sharedStyles = new SharedStyles();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Before any window exists, so their shortcuts are created in the platform's form.
            PlatformShortcuts.Install();

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

            InstallCrashHandlers(viewModel);

            desktop.ShutdownRequested += (s, e) =>
            {
                viewModel.FlushAllCaches();
                viewModel.SaveState();
                fileChangeService.Dispose();
            };

            if (OperatingSystem.IsMacOS())
                SetUpMacApp(desktop, viewModel);

            // Single-instance: open files forwarded from later launches in this instance.
            SingleInstanceManager.StartServer(receivedArgs =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    HandleForwardedArgs(desktop, viewModel, receivedArgs)));
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Last line of defence: an unexpected exception is logged to ~/.notemode/error.log and every
    /// tab's pending edits are written to the cache first, so nothing typed is lost. UI-thread
    /// errors are then swallowed to keep the editor open; a background-thread crash cannot be.
    /// </summary>
    private static void InstallCrashHandlers(MainWindowViewModel viewModel)
    {
        void Preserve(Exception? ex)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppPaths.DataDir, "error.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] NoteMode {typeof(App).Assembly.GetName().Version}\n{ex}\n\n");
            }
            catch { /* nothing more to do */ }
            try
            {
                viewModel.FlushAllCaches();
                viewModel.SaveState();
            }
            catch { /* nothing more to do */ }
        }

        Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Preserve(e.Exception);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Preserve(e.ExceptionObject as Exception);
    }

    /// <summary>
    /// macOS: Finder opens files (double-click, Open With, drop on the Dock icon) with an Apple Event
    /// to the running app rather than command-line arguments, and the application menu (NoteMode >
    /// About, Settings) is the native one rather than part of the window.
    /// </summary>
    private void SetUpMacApp(IClassicDesktopStyleApplicationLifetime desktop, MainWindowViewModel viewModel)
    {
        if (this.TryGetFeature<IActivatableLifetime>() is { } activatable)
        {
            activatable.Activated += (_, e) =>
            {
                if (e is FileActivatedEventArgs fileArgs)
                {
                    var paths = fileArgs.Files
                        .Select(f => f.TryGetLocalPath())
                        .OfType<string>()
                        .ToArray();
                    HandleForwardedArgs(desktop, viewModel, paths);
                }
            };
        }

        var about = new NativeMenuItem("About NoteMode");
        about.Click += (_, _) => (desktop.MainWindow as MainWindow)?.ShowSettings();
        var settings = new NativeMenuItem("Settings...")
        {
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Meta)
        };
        settings.Click += (_, _) => (desktop.MainWindow as MainWindow)?.ShowSettings();
        // Avalonia adds the standard Services / Hide / Quit (Cmd+Q) items after these.
        NativeMenu.SetMenu(this, new NativeMenu { about, new NativeMenuItemSeparator(), settings });
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

        if (_sharedStyles != null)
        {
            Styles.Remove(_sharedStyles);
        }

        // Add new theme, then the shared structural styles so they take precedence over it
        var newTheme = useLightTheme ? _lightTheme : _draculaTheme;
        if (newTheme != null)
        {
            Styles.Add(newTheme);
        }
        if (_sharedStyles != null)
        {
            Styles.Add(_sharedStyles);
        }

        // Update syntax highlighting colors
        _syntaxService?.SetLightTheme(useLightTheme);

        // Raise event for other components to react
        ThemeChanged?.Invoke(this, useLightTheme);
    }

    public event EventHandler<bool>? ThemeChanged;
}
