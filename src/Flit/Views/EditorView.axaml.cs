using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using Flit.ViewModels;

namespace Flit.Views;

public partial class EditorView : UserControl
{
    private TextEditor? _editor;
    private TabViewModel? _viewModel;
    private MainWindowViewModel? _mainViewModel;
    private bool _isUpdatingFromViewModel;
    private bool _settingsApplied;
    private MarkdownTransformer? _markdownTransformer;

    private const double MinFontSize = 6;
    private const double MaxFontSize = 72;
    private const double ZoomStep = 2;

    // Selection brush colors for different themes
    private static readonly SolidColorBrush DarkSelectionBrush = new(Color.Parse("#6B4A1A"));
    private static readonly SolidColorBrush LightSelectionBrush = new(Color.Parse("#ADD6FF"));

    // Link text colors for different themes
    private static readonly SolidColorBrush DarkLinkBrush = new(Color.Parse("#8be9fd"));
    private static readonly SolidColorBrush LightLinkBrush = new(Color.Parse("#0066cc"));

    public EditorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _editor = this.FindControl<TextEditor>("Editor");
        if (_editor != null)
        {
            _editor.TextChanged += OnEditorTextChanged;
            // Use tunneling to catch Ctrl+Wheel before scrollbar handles it
            _editor.AddHandler(PointerWheelChangedEvent, OnEditorPointerWheelChanged, RoutingStrategies.Tunnel);
            _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            ApplySelectionBrush();

            // Add spacing after line numbers
            foreach (var margin in _editor.TextArea.LeftMargins)
            {
                if (margin is LineNumberMargin lineNumberMargin)
                {
                    lineNumberMargin.Margin = new Thickness(0, 0, 12, 0);
                }
            }

            // Add padding to text content
            _editor.TextArea.Padding = new Thickness(8);
            _editor.TextArea.TextView.Margin = new Thickness(8);
        }

        // Subscribe to theme changes
        if (App.Instance != null)
        {
            App.Instance.ThemeChanged += OnAppThemeChanged;
        }
    }

    private void OnAppThemeChanged(object? sender, bool isLightTheme)
    {
        Dispatcher.UIThread.Post(ApplyThemeBrushes);
    }

    private void ApplySelectionBrush()
    {
        ApplyThemeBrushes();
    }

    private void ApplyThemeBrushes()
    {
        if (_editor != null)
        {
            var isLight = App.Instance?.IsLightTheme ?? false;
            _editor.TextArea.SelectionBrush = isLight ? LightSelectionBrush : DarkSelectionBrush;
            _editor.TextArea.TextView.LinkTextForegroundBrush = isLight ? LightLinkBrush : DarkLinkBrush;

            if (_markdownTransformer != null)
            {
                _markdownTransformer.SetLightTheme(isLight);
                _editor.TextArea.TextView.Redraw();
            }
        }
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Get the main view model from the window
        var window = this.GetVisualRoot() as Window;
        _mainViewModel = window?.DataContext as MainWindowViewModel;

        if (_mainViewModel != null && !_settingsApplied)
        {
            _mainViewModel.PropertyChanged += OnMainViewModelPropertyChanged;

            // Apply settings after a short delay to ensure editor is fully ready
            Dispatcher.UIThread.Post(() =>
            {
                ApplyEditorSettings();
                _settingsApplied = true;
            }, DispatcherPriority.Loaded);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (_mainViewModel != null)
        {
            _mainViewModel.PropertyChanged -= OnMainViewModelPropertyChanged;
        }
        if (App.Instance != null)
        {
            App.Instance.ThemeChanged -= OnAppThemeChanged;
        }
        if (_markdownTransformer != null && _editor != null)
        {
            _editor.TextArea.TextView.LineTransformers.Remove(_markdownTransformer);
            _markdownTransformer = null;
        }
        _settingsApplied = false;
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ShowWhitespace))
        {
            ApplyWhitespaceSetting();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.FontSize))
        {
            ApplyFontSize();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ShowLineNumbers))
        {
            ApplyLineNumbersSetting();
        }
    }

    private void ApplyEditorSettings()
    {
        if (_editor == null || _mainViewModel == null)
            return;

        ApplyFontSize();
        ApplyWhitespaceSetting();
        ApplyLineNumbersSetting();
    }

    private void ApplyFontSize()
    {
        if (_editor != null && _mainViewModel != null && _mainViewModel.FontSize > 0)
        {
            _editor.FontSize = _mainViewModel.FontSize;
            if (_markdownTransformer != null)
            {
                _markdownTransformer.SetBaseFontSize(_mainViewModel.FontSize);
                _editor.TextArea.TextView.Redraw();
            }
        }
    }

    private void ApplyWhitespaceSetting()
    {
        if (_editor?.Options != null && _mainViewModel != null)
        {
            _editor.Options.ShowSpaces = _mainViewModel.ShowWhitespace;
            _editor.Options.ShowTabs = _mainViewModel.ShowWhitespace;
        }
    }

    private void ApplyLineNumbersSetting()
    {
        if (_editor != null && _mainViewModel != null)
        {
            _editor.ShowLineNumbers = _mainViewModel.ShowLineNumbers;
        }
    }

    private void UpdateMarkdownTransformer()
    {
        if (_editor == null)
            return;

        var isMarkdown = _viewModel?.SyntaxName == "MarkDown";

        if (isMarkdown)
        {
            if (_markdownTransformer == null)
            {
                _markdownTransformer = new MarkdownTransformer();
                _markdownTransformer.SetLightTheme(App.Instance?.IsLightTheme ?? false);
                _markdownTransformer.SetBaseFontSize(_editor.FontSize);
                _editor.TextArea.TextView.LineTransformers.Add(_markdownTransformer);
            }
        }
        else
        {
            if (_markdownTransformer != null)
            {
                _editor.TextArea.TextView.LineTransformers.Remove(_markdownTransformer);
                _markdownTransformer = null;
            }
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_editor != null && _mainViewModel != null)
        {
            var caret = _editor.TextArea.Caret;
            _mainViewModel.StatusBar.Line = caret.Line;
            _mainViewModel.StatusBar.Column = caret.Column;
        }
    }

    private void UpdateStatusBarLineEnding()
    {
        if (_editor != null && _mainViewModel != null)
        {
            var text = _editor.Text;
            if (text.Contains("\r\n"))
            {
                _mainViewModel.StatusBar.LineEnding = "CRLF";
            }
            else if (text.Contains('\n'))
            {
                _mainViewModel.StatusBar.LineEnding = "LF";
            }
            else if (text.Contains('\r'))
            {
                _mainViewModel.StatusBar.LineEnding = "CR";
            }
            else
            {
                _mainViewModel.StatusBar.LineEnding = "LF";
            }
        }
    }

    private void OnEditorPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_editor == null)
            return;

        // Check if Ctrl is pressed
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var delta = e.Delta.Y;
            var currentSize = _editor.FontSize;
            double newSize = currentSize;

            if (delta > 0)
            {
                // Zoom in
                newSize = Math.Min(currentSize + ZoomStep, MaxFontSize);
            }
            else if (delta < 0)
            {
                // Zoom out
                newSize = Math.Max(currentSize - ZoomStep, MinFontSize);
            }

            _editor.FontSize = newSize;

            // Save to main view model
            if (_mainViewModel != null)
            {
                _mainViewModel.FontSize = newSize;
            }

            if (_markdownTransformer != null)
            {
                _markdownTransformer.SetBaseFontSize(newSize);
                _editor.TextArea.TextView.Redraw();
            }

            e.Handled = true;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as TabViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateEditorContent();
            UpdateMarkdownTransformer();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabViewModel.Content) && !_isUpdatingFromViewModel)
        {
            UpdateEditorContent();
        }
        else if (e.PropertyName == nameof(TabViewModel.SyntaxName))
        {
            UpdateMarkdownTransformer();
        }
    }

    private void UpdateEditorContent()
    {
        if (_editor != null && _viewModel != null)
        {
            _isUpdatingFromViewModel = true;
            if (_editor.Text != _viewModel.Content)
            {
                _editor.Text = _viewModel.Content;
            }
            _isUpdatingFromViewModel = false;
            UpdateStatusBarLineEnding();
        }
    }

    private void OnEditorTextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null && _editor != null && !_isUpdatingFromViewModel)
        {
            _viewModel.Content = _editor.Text;
        }
    }

    public void Undo()
    {
        if (_editor?.Document.UndoStack.CanUndo == true)
        {
            _editor.Document.UndoStack.Undo();
        }
    }

    public void Redo()
    {
        if (_editor?.Document.UndoStack.CanRedo == true)
        {
            _editor.Document.UndoStack.Redo();
        }
    }

    public TextEditor? GetEditor() => _editor;
}
