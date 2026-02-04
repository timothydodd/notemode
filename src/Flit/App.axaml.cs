using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Flit.Services;
using Flit.ViewModels;
using Flit.Views;

namespace Flit;

public partial class App : Application
{
    private Styles? _draculaTheme;
    private Styles? _lightTheme;
    private SyntaxService? _syntaxService;

    public static App? Instance => Current as App;

    public bool IsLightTheme { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Load both themes
        _draculaTheme = (Styles)AvaloniaXamlLoader.Load(new Uri("avares://Flit/Themes/Dracula.axaml"));
        _lightTheme = (Styles)AvaloniaXamlLoader.Load(new Uri("avares://Flit/Themes/Light.axaml"));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var stateService = new StateService();
            var cacheService = new CacheService();
            _syntaxService = new SyntaxService();
            var fileChangeService = new FileChangeService();

            // Load initial theme based on saved preference
            var state = stateService.LoadState();
            ApplyTheme(state.UseLightTheme);

            var viewModel = new MainWindowViewModel(stateService, cacheService, _syntaxService, fileChangeService);

            // Subscribe to theme changes
            viewModel.ThemeChanged += (s, useLightTheme) => ApplyTheme(useLightTheme);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            // Open files passed via command line arguments
            if (desktop.Args != null)
            {
                foreach (var arg in desktop.Args)
                {
                    if (!string.IsNullOrEmpty(arg) && File.Exists(arg))
                    {
                        viewModel.OpenFile(arg);
                    }
                }
            }

            desktop.ShutdownRequested += (s, e) =>
            {
                viewModel.SaveState();
                fileChangeService.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
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
