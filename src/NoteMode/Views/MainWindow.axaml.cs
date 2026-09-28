using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using NoteMode.ViewModels;

namespace NoteMode.Views;

public partial class MainWindow : Window
{
    public static readonly DirectProperty<MainWindow, bool> IsEphemeralModeProperty =
        AvaloniaProperty.RegisterDirect<MainWindow, bool>(
            nameof(IsEphemeralMode),
            o => o.IsEphemeralMode);

    private bool _isEphemeralMode;
    public bool IsEphemeralMode
    {
        get => _isEphemeralMode;
        private set => SetAndRaise(IsEphemeralModeProperty, ref _isEphemeralMode, value);
    }

    private TabViewModel? _draggedTab;
    private Control? _draggedElement;
    private Point _dragStartPoint;
    private bool _isDragging;
    private int _draggedOriginalIndex;
    private int _currentDropIndex;
    private const double DragThreshold = 8;

    private Border? _dragGhost;
    private TextBlock? _dragGhostText;
    private Canvas? _dragCanvas;
    private ListBox? _tabStrip;
    private SearchPanel? _searchPanel;
    private SearchPanelViewModel? _searchPanelViewModel;
    private ColumnDefinition? _searchPanelColumn;
    private ColumnDefinition? _searchSplitterColumn;
    private GridSplitter? _searchSplitter;
    private NotesPanel? _notesPanel;
    private NotesPanelViewModel? _notesPanelViewModel;
    private ColumnDefinition? _notesPanelColumn;
    private ColumnDefinition? _notesSplitterColumn;
    private GridSplitter? _notesSplitter;
    private ExplorerPanel? _explorerPanel;
    private ExplorerPanelViewModel? _explorerPanelViewModel;
    private RowDefinition? _searchPanelRow;
    private RowDefinition? _explorerSplitterRow;
    private RowDefinition? _explorerPanelRow;
    private GridSplitter? _leftPanelSplitter;

    public MainWindow()
    {
        InitializeComponent();
        OpenFileCommand = new RelayCommand(_ => RunAsync(OpenFileAsync));
        UndoCommand = new RelayCommand(_ => GetCurrentEditorView()?.Undo());
        RedoCommand = new RelayCommand(_ => GetCurrentEditorView()?.Redo());
        TogglePreviewCommand = new RelayCommand(_ => GetCurrentEditorView()?.TogglePreview());
        CloseTabWithPromptCommand = new RelayCommand(tab => RunAsync(() => TryCloseTabAsync(tab as TabViewModel)));
        PinTabCommand = new RelayCommand(tab => ViewModel?.PinEphemeralTab(tab as TabViewModel));
        FindCommand = new RelayCommand(_ => ShowFindReplaceDialog(replace: false));
        ReplaceCommand = new RelayCommand(_ => ShowFindReplaceDialog(replace: true));

        AddHandler(PointerMovedEvent, Window_PointerMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, Window_PointerReleased, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control)
        {
            RunAsync(SaveFileAsync);
            e.Handled = true;
        }
        else if (e.Key == Key.S && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            RunAsync(SaveAsAsync);
            e.Handled = true;
        }
        else if (e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ToggleSearchPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.E && e.KeyModifiers == KeyModifiers.Control)
        {
            ToggleExplorerPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.V && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            GetCurrentEditorView()?.TogglePreview();
            e.Handled = true;
        }
        else if (e.Key == Key.N && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ToggleNotesPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && ViewModel?.IsSearchPanelOpen == true && _searchPanel != null)
        {
            // Close search panel if it (or its children) has focus
            if (_searchPanel.IsKeyboardFocusWithin)
            {
                ViewModel.IsSearchPanelOpen = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape && ViewModel?.IsExplorerPanelOpen == true && _explorerPanel != null)
        {
            if (_explorerPanel.IsKeyboardFocusWithin)
            {
                ViewModel.IsExplorerPanelOpen = false;
                e.Handled = true;
            }
        }
        else if (e.Key == Key.Escape && ViewModel?.IsNotesPanelOpen == true && _notesPanel != null)
        {
            if (_notesPanel.IsKeyboardFocusWithin)
            {
                ViewModel.IsNotesPanelOpen = false;
                e.Handled = true;
            }
        }
    }

    private async void RunAsync(Func<System.Threading.Tasks.Task> asyncAction)
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            await ShowMessageAsync("Something went wrong", ex.Message);
        }
    }

    private System.Threading.Tasks.Task ShowMessageAsync(string title, string message) =>
        new MessageDialog(title, message).ShowDialog(this);

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _dragGhost = this.FindControl<Border>("DragGhost");
        _dragGhostText = this.FindControl<TextBlock>("DragGhostText");
        _dragCanvas = this.FindControl<Canvas>("DragCanvas");
        _tabStrip = this.FindControl<ListBox>("TabStrip");

        if (ViewModel != null)
        {
            ViewModel.ExternalChangeDetected += OnExternalChangeDetected;
            ViewModel.ErrorOccurred += (_, error) => RunAsync(() => ShowMessageAsync(error.Title, error.Message));
            IsEphemeralMode = ViewModel.IsEphemeralMode;
            ViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MainWindowViewModel.IsEphemeralMode))
                    IsEphemeralMode = ViewModel.IsEphemeralMode;
            };
            RestoreWindowPosition();
            InitializeSearchPanel();
            InitializeExplorerPanel();
            InitializeNotesPanel();
        }
    }

    private void RestoreWindowPosition()
    {
        if (ViewModel == null)
            return;

        // Restore the saved size, but always open centered on screen
        // (WindowStartupLocation="CenterScreen" is set in XAML).
        Width = ViewModel.WindowWidth;
        Height = ViewModel.WindowHeight;

        if (ViewModel.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void UpdateWindowBoundsOnViewModel()
    {
        if (ViewModel == null)
            return;

        ViewModel.IsMaximized = WindowState == WindowState.Maximized;

        // Save the normal (non-maximized) bounds so we restore to the right position
        if (WindowState == WindowState.Normal)
        {
            ViewModel.WindowWidth = Width;
            ViewModel.WindowHeight = Height;
            ViewModel.WindowX = Position.X;
            ViewModel.WindowY = Position.Y;
        }
    }

    private void OnExternalChangeDetected(object? sender, TabViewModel tab)
    {
        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await HandleExternalChangeAsync(tab);
        });
    }

    private async System.Threading.Tasks.Task HandleExternalChangeAsync(TabViewModel tab)
    {
        // A restored tab that has not been shown yet may still hold cached edits.
        tab.EnsureContentLoaded();
        if (!tab.IsDirty)
        {
            // Clean file: auto-reload silently
            tab.ReloadFromDisk();
            return;
        }

        // Dirty file: show dialog
        var dialog = new FileChangedDialog(tab.Title);
        var result = await dialog.ShowDialog<FileChangedResult>(this);

        if (result == FileChangedResult.Reload)
            tab.ReloadFromDisk();
        else
            tab.AcknowledgeExternalChanges();
    }

    public ICommand OpenFileCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand TogglePreviewCommand { get; }
    public ICommand CloseTabWithPromptCommand { get; }
    public ICommand PinTabCommand { get; }
    public ICommand FindCommand { get; }
    public ICommand ReplaceCommand { get; }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private EditorView? GetCurrentEditorView()
    {
        // Find the EditorView in the content area
        return this.GetVisualDescendants().OfType<EditorView>().FirstOrDefault();
    }

    private void TabItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Grid grid && grid.Tag is TabViewModel tab)
        {
            if (e.GetCurrentPoint(grid).Properties.IsLeftButtonPressed)
            {
                _draggedTab = tab;
                _draggedElement = grid;
                _dragStartPoint = e.GetPosition(this);
                _isDragging = false;
                _draggedOriginalIndex = ViewModel?.Tabs.IndexOf(tab) ?? -1;
                _currentDropIndex = _draggedOriginalIndex;
            }
        }
    }

    private void Window_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTab == null || _draggedElement == null)
            return;

        var currentPoint = e.GetPosition(this);
        var diff = currentPoint - _dragStartPoint;

        // Check if we should start dragging
        if (!_isDragging && (Math.Abs(diff.X) > DragThreshold || Math.Abs(diff.Y) > DragThreshold))
        {
            StartDrag();
        }

        if (_isDragging)
        {
            UpdateDragGhostPosition(currentPoint);
            UpdateDropIndicator(currentPoint);
        }
    }

    private void StartDrag()
    {
        _isDragging = true;

        // Show ghost
        if (_dragGhost != null && _dragGhostText != null && _draggedTab != null)
        {
            _dragGhostText.Text = _draggedTab.DisplayTitle;
            _dragGhost.IsVisible = true;
        }

        // Hide original tab visually
        if (_draggedElement != null)
        {
            _draggedElement.Opacity = 0.3;
        }
    }

    private void UpdateDragGhostPosition(Point mousePos)
    {
        if (_dragGhost == null)
            return;

        // Offset the ghost slightly from cursor
        Canvas.SetLeft(_dragGhost, mousePos.X + 10);
        Canvas.SetTop(_dragGhost, mousePos.Y - 10);
    }

    private void UpdateDropIndicator(Point mousePos)
    {
        if (ViewModel == null || _tabStrip == null)
            return;

        // Find all tab item containers
        var tabItems = GetTabItemContainers();
        if (tabItems.Count == 0)
            return;

        int newDropIndex = ViewModel.Tabs.Count; // Default to end

        for (int i = 0; i < tabItems.Count; i++)
        {
            var tabItem = tabItems[i];
            var bounds = tabItem.Bounds;
            var tabPos = tabItem.TranslatePoint(new Point(0, 0), this);

            if (tabPos.HasValue)
            {
                var tabMidX = tabPos.Value.X + bounds.Width / 2;

                if (mousePos.X < tabMidX)
                {
                    newDropIndex = i;
                    break;
                }
            }
        }

        if (newDropIndex != _currentDropIndex)
        {
            _currentDropIndex = newDropIndex;
            UpdateTabMargins(tabItems, newDropIndex);
        }
    }

    private void UpdateTabMargins(List<ListBoxItem> tabItems, int dropIndex)
    {
        for (int i = 0; i < tabItems.Count; i++)
        {
            var tabItem = tabItems[i];
            var vm = tabItem.DataContext as TabViewModel;

            if (vm == _draggedTab)
            {
                // Keep dragged tab small/hidden
                tabItem.Margin = new Thickness(0);
                continue;
            }

            // Add gap before the drop position
            if (i == dropIndex && dropIndex != _draggedOriginalIndex)
            {
                tabItem.Margin = new Thickness(60, 0, 0, 0); // Gap on left
            }
            else if (i == dropIndex - 1 && dropIndex > _draggedOriginalIndex && dropIndex == tabItems.Count)
            {
                tabItem.Margin = new Thickness(0, 0, 60, 0); // Gap on right for end position
            }
            else
            {
                tabItem.Margin = new Thickness(0);
            }
        }
    }

    private List<ListBoxItem> GetTabItemContainers()
    {
        var result = new List<ListBoxItem>();
        if (_tabStrip == null)
            return result;

        // Find all ListBoxItems in the tab strip
        foreach (var item in _tabStrip.GetVisualDescendants().OfType<ListBoxItem>())
        {
            result.Add(item);
        }

        return result;
    }

    private void Window_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isDragging && _draggedTab != null && ViewModel != null)
        {
            // Perform the actual move
            var sourceIndex = ViewModel.Tabs.IndexOf(_draggedTab);
            var targetIndex = _currentDropIndex;

            // Adjust target index if needed
            if (sourceIndex < targetIndex)
            {
                targetIndex--;
            }

            if (sourceIndex != targetIndex && targetIndex >= 0 && targetIndex < ViewModel.Tabs.Count)
            {
                ViewModel.Tabs.Move(sourceIndex, targetIndex);
                ViewModel.SaveState();
            }

            // Select the dragged tab after drop
            ViewModel.SelectedTab = _draggedTab;
        }

        EndDrag();
    }

    private void EndDrag()
    {
        // Hide ghost
        if (_dragGhost != null)
        {
            _dragGhost.IsVisible = false;
        }

        // Restore original tab opacity
        if (_draggedElement != null)
        {
            _draggedElement.Opacity = 1.0;
        }

        // Reset all tab margins
        var tabItems = GetTabItemContainers();
        foreach (var tabItem in tabItems)
        {
            tabItem.Margin = new Thickness(0);
        }

        _draggedTab = null;
        _draggedElement = null;
        _isDragging = false;
        _draggedOriginalIndex = -1;
        _currentDropIndex = -1;
    }

    private async System.Threading.Tasks.Task OpenFileAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open File",
            AllowMultiple = true,
            FileTypeFilter = new List<FilePickerFileType>
            {
                FilePickerFileTypes.All
            }
        });

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                ViewModel?.OpenFile(path);
            }
        }
    }

    private System.Threading.Tasks.Task SaveFileAsync() => SaveTabAsync(ViewModel?.SelectedTab);

    private System.Threading.Tasks.Task SaveAsAsync() => SaveTabAsAsync(ViewModel?.SelectedTab);

    /// <summary>Saves a tab (asking for a path if it has none). False when cancelled or the save failed.</summary>
    private async System.Threading.Tasks.Task<bool> SaveTabAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null)
            return false;

        if (!tab.IsNote && string.IsNullOrEmpty(tab.FilePath))
            return await SaveTabAsAsync(tab);

        return ViewModel.SaveFile(tab);
    }

    /// <summary>Asks for a path and saves the tab there. False when cancelled or the save failed.</summary>
    private async System.Threading.Tasks.Task<bool> SaveTabAsAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null)
            return false;

        var suggestedName = tab.Title;
        if (string.IsNullOrEmpty(System.IO.Path.GetExtension(suggestedName)))
        {
            suggestedName += ".txt";
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = suggestedName,
            SuggestedStartLocation = string.IsNullOrEmpty(tab.FilePath)
                ? null
                : await StorageProvider.TryGetFolderFromPathAsync(System.IO.Path.GetDirectoryName(tab.FilePath)!),
            DefaultExtension = "txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        var path = file?.TryGetLocalPath();
        return !string.IsNullOrEmpty(path) && ViewModel.SaveFile(tab, path);
    }

    private async System.Threading.Tasks.Task RenameTabAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        var dialog = new RenameDialog(tab.Title);
        var result = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrEmpty(result))
        {
            ViewModel?.RenameTab(tab, result);
        }
    }

    private async void SaveTab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await SaveTabAsync(tab);
        }
    }

    private async void SaveTabAs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await SaveTabAsAsync(tab);
        }
    }

    private async void RenameTab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await RenameTabAsync(tab);
        }
    }

    private async void CloseTab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await TryCloseTabAsync(tab);
        }
    }

    private async void CloseOthers_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await TryCloseOthersAsync(tab);
        }
    }

    private async void CloseToRight_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await TryCloseToRightAsync(tab);
        }
    }

    private async void CloseToLeft_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            await TryCloseToLeftAsync(tab);
        }
    }

    private void CloseUnchanged_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel?.CloseUnchanged();
    }

    private async void CloseAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await TryCloseAllAsync();
    }

    private async void CloseTab_MenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel?.SelectedTab != null)
            await TryCloseTabAsync(ViewModel.SelectedTab);
    }

    private async void CloseAll_MenuItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await TryCloseAllAsync();
    }

    private void Exit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private async void OpenFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await OpenFileAsync();
    }

    private async void SaveFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveFileAsync();
    }

    private async void SaveAs_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    // Window control handlers
    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void Minimize_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    // Lucide "maximize" and "minimize": the title bar button shows what clicking it will do.
    private const string MaximizeIconData = "M8 3H5a2 2 0 0 0-2 2v3 M21 8V5a2 2 0 0 0-2-2h-3 M3 16v3a2 2 0 0 0 2 2h3 M16 21h3a2 2 0 0 0 2-2v-3";
    private const string RestoreIconData = "M8 3v3a2 2 0 0 1-2 2H3 M21 8h-3a2 2 0 0 1-2-2V3 M3 16h3a2 2 0 0 1 2 2v3 M16 21v-3a2 2 0 0 1 2-2h3";

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
            UpdateMaximizeButton();
    }

    private void UpdateMaximizeButton()
    {
        var maximized = WindowState == WindowState.Maximized;
        if (this.FindControl<Avalonia.Controls.Shapes.Path>("MaximizeIcon") is { } icon)
            icon.Data = Avalonia.Media.Geometry.Parse(maximized ? RestoreIconData : MaximizeIconData);
        if (this.FindControl<Button>("MaximizeButton") is { } button)
        {
            var label = maximized ? "Restore" : "Maximize";
            ToolTip.SetTip(button, label);
            Avalonia.Automation.AutomationProperties.SetName(button, label);
        }
    }

    private void MaximizeRestore_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
        }
        else
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void Close_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void Undo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetCurrentEditorView()?.Undo();
    }

    private void Cut_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => GetCurrentEditorView()?.GetEditor()?.Cut();

    private void Copy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => GetCurrentEditorView()?.GetEditor()?.Copy();

    private void Paste_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => GetCurrentEditorView()?.GetEditor()?.Paste();

    private void SelectAll_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => GetCurrentEditorView()?.GetEditor()?.SelectAll();

    private async void CopyPath_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: TabViewModel { FilePath: { Length: > 0 } path } } && Clipboard != null)
            await Clipboard.SetTextAsync(path);
    }

    private void RevealInExplorer_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: TabViewModel { FilePath: { Length: > 0 } path } })
            return;
        try
        {
            if (OperatingSystem.IsWindows())
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
            else
                Launcher.LaunchDirectoryInfoAsync(new System.IO.FileInfo(path).Directory!);
        }
        catch (Exception ex)
        {
            RunAsync(() => ShowMessageAsync("Can't open folder", ex.Message));
        }
    }

    private void Redo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetCurrentEditorView()?.Redo();
    }

    public async System.Threading.Tasks.Task<bool> TryCloseTabAsync(TabViewModel? tab)
    {
        if (tab == null)
            return true;

        // Load a restored tab's cached edits so IsDirty tells the truth before asking.
        tab.EnsureContentLoaded();
        if (tab.IsDirty)
        {
            ViewModel!.SelectedTab = tab;
            var dialog = new UnsavedChangesDialog(tab.Title);
            var result = await dialog.ShowDialog<UnsavedChangesResult>(this);

            switch (result)
            {
                case UnsavedChangesResult.Cancel:
                    return false;
                case UnsavedChangesResult.Save:
                    // Keep the tab (and its edits) open if the save was cancelled or failed.
                    if (!await SaveTabAsync(tab))
                        return false;
                    break;
            }
        }

        ViewModel?.CloseTab(tab);
        return true;
    }

    private async System.Threading.Tasks.Task TryCloseOthersAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null)
            return;

        var tabsToClose = ViewModel.Tabs.Where(t => t != tab).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseToRightAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null)
            return;

        var index = ViewModel.Tabs.IndexOf(tab);
        if (index < 0)
            return;

        var tabsToClose = ViewModel.Tabs.Skip(index + 1).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseToLeftAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null)
            return;

        var index = ViewModel.Tabs.IndexOf(tab);
        if (index <= 0)
            return;

        var tabsToClose = ViewModel.Tabs.Take(index).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseAllAsync()
    {
        if (ViewModel == null)
            return;

        var tabsToClose = ViewModel.Tabs.ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private FindReplaceDialog? _findReplaceDialog;

    private void ShowFindReplaceDialog(bool replace)
    {
        var editor = GetCurrentEditorView()?.GetEditor();
        if (editor == null)
            return;

        // One dialog at a time: Ctrl+F / Ctrl+H again brings it back instead of stacking another.
        if (_findReplaceDialog != null)
        {
            if (_findReplaceDialog.Editor == editor)
            {
                _findReplaceDialog.Activate();
                _findReplaceDialog.FocusField(replace);
                return;
            }
            _findReplaceDialog.Close();
        }

        var dialog = new FindReplaceDialog(editor) { FocusReplace = replace };
        dialog.Closed += (_, _) =>
        {
            if (_findReplaceDialog == dialog)
                _findReplaceDialog = null;
        };
        _findReplaceDialog = dialog;
        dialog.Show(this);
    }

    private void GoToSearchResult(TabViewModel tab, int offset, int length, int line)
    {
        if (ViewModel == null)
            return;

        // Switch to the tab
        ViewModel.SelectedTab = tab;

        // Need to wait for the tab to be rendered before selecting text
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var editorView = GetCurrentEditorView();
            var editor = editorView?.GetEditor();
            if (editor != null)
            {
                editor.Select(offset, length);
                editor.CaretOffset = offset + length;
                var location = editor.Document.GetLocation(offset);
                editor.ScrollTo(location.Line, location.Column);
                editor.Focus();
            }
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void Find_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowFindReplaceDialog(replace: false);
    }

    private void ToggleSearchPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleSearchPanel();
    }

    private void ToggleSearchPanel()
    {
        if (ViewModel == null)
            return;

        ViewModel.IsSearchPanelOpen = !ViewModel.IsSearchPanelOpen;

        if (ViewModel.IsSearchPanelOpen)
        {
            // Focus the search box after opening
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                _searchPanel?.FocusSearchBox();
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }

    private void InitializeSearchPanel()
    {
        if (ViewModel == null)
            return;

        _searchPanel = this.FindControl<SearchPanel>("SearchPanelControl");
        _searchSplitter = this.FindControl<GridSplitter>("SearchSplitter");
        // ColumnDefinitions are on the outer content Grid (parent of LeftPanelGrid)
        var leftPanelGrid = this.FindControl<Grid>("LeftPanelGrid");
        var contentGrid = leftPanelGrid?.Parent as Grid;
        if (contentGrid?.ColumnDefinitions.Count >= 2)
        {
            _searchPanelColumn = contentGrid.ColumnDefinitions[0];
            _searchSplitterColumn = contentGrid.ColumnDefinitions[1];
        }
        if (_searchPanel == null)
            return;

        _searchPanelViewModel = new SearchPanelViewModel(ViewModel);
        _searchPanel.DataContext = _searchPanelViewModel;

        // Set initial state
        if (ViewModel.IsSearchPanelOpen)
        {
            ShowSearchPanel();
        }

        // React to ViewModel changes
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsSearchPanelOpen))
            {
                if (ViewModel.IsSearchPanelOpen)
                    ShowSearchPanel();
                else
                    HideSearchPanel();
            }
        };

        _searchPanel.CloseRequested += (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.IsSearchPanelOpen = false;
        };

        _searchPanel.BrowseFolderRequested += async (s, e) =>
        {
            await BrowseSearchFolderAsync();
        };

        _searchPanel.ResultSelected += (s, item) =>
        {
            if (item == null)
                return;
            NavigateToSearchResult(item);
        };
    }

    private void ShowSearchPanel()
    {
        UpdateLeftPanelLayout();
    }

    private void HideSearchPanel()
    {
        if (_searchPanelColumn == null)
            return;
        // Save current width before hiding
        if (ViewModel != null)
            ViewModel.SearchPanelWidth = _searchPanelColumn.Width.Value;
        UpdateLeftPanelLayout();
    }

    private async System.Threading.Tasks.Task BrowseSearchFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Select Folder to Search",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(path) && _searchPanelViewModel != null)
            {
                _searchPanelViewModel.FolderPath = path;
            }
        }
    }

    private void NavigateToSearchResult(SearchResultItem item)
    {
        if (ViewModel == null)
            return;

        if (item.Tab != null)
        {
            // Tab mode: switch to the tab and navigate
            GoToSearchResult(item.Tab, item.StartOffset, item.Length, item.LineNumber);
        }
        else if (!string.IsNullOrEmpty(item.FilePath))
        {
            // File mode: open file (or switch to existing tab) and navigate
            var tab = ViewModel.OpenFile(item.FilePath);
            if (tab != null)
                GoToSearchResult(tab, item.StartOffset, item.Length, item.LineNumber);
        }
    }

    // --- Explorer Panel ---

    private void InitializeExplorerPanel()
    {
        if (ViewModel == null)
            return;

        _explorerPanel = this.FindControl<ExplorerPanel>("ExplorerPanelControl");
        _leftPanelSplitter = this.FindControl<GridSplitter>("LeftPanelSplitter");

        // Find row definitions from the left panel grid
        var leftPanelGrid = this.FindControl<Grid>("LeftPanelGrid");
        if (leftPanelGrid != null)
        {
            _searchPanelRow = leftPanelGrid.RowDefinitions.Count > 0 ? leftPanelGrid.RowDefinitions[0] : null;
            _explorerSplitterRow = leftPanelGrid.RowDefinitions.Count > 1 ? leftPanelGrid.RowDefinitions[1] : null;
            _explorerPanelRow = leftPanelGrid.RowDefinitions.Count > 2 ? leftPanelGrid.RowDefinitions[2] : null;
        }

        if (_explorerPanel == null)
            return;

        _explorerPanelViewModel = new ExplorerPanelViewModel(ViewModel);
        _explorerPanel.DataContext = _explorerPanelViewModel;

        // Set initial state
        if (ViewModel.IsExplorerPanelOpen)
        {
            ShowExplorerPanel();
        }
        else
        {
            HideExplorerPanel();
        }

        // Update explorer root when selected tab changes
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsExplorerPanelOpen))
            {
                if (ViewModel.IsExplorerPanelOpen)
                    ShowExplorerPanel();
                else
                    HideExplorerPanel();
            }
            else if (e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
            {
                if (ViewModel.IsExplorerPanelOpen)
                {
                    _explorerPanelViewModel?.UpdateRoot(ViewModel.SelectedTab?.FilePath);
                }
            }
        };

        _explorerPanel.CloseRequested += (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.IsExplorerPanelOpen = false;
        };

        _explorerPanel.FileOpened += (s, path) =>
        {
            if (!string.IsNullOrEmpty(path))
                ViewModel?.OpenFile(path);
        };
    }

    private void ShowExplorerPanel()
    {
        UpdateLeftPanelLayout();

        // Update root from current tab
        if (ViewModel?.SelectedTab != null)
        {
            _explorerPanelViewModel?.UpdateRoot(ViewModel.SelectedTab.FilePath);
        }
    }

    private void HideExplorerPanel()
    {
        UpdateLeftPanelLayout();
    }

    private void ToggleExplorerPanel()
    {
        if (ViewModel == null)
            return;
        ViewModel.IsExplorerPanelOpen = !ViewModel.IsExplorerPanelOpen;
    }

    private void UpdateLeftPanelLayout()
    {
        bool searchOpen = ViewModel?.IsSearchPanelOpen == true;
        bool explorerOpen = ViewModel?.IsExplorerPanelOpen == true;
        bool eitherOpen = searchOpen || explorerOpen;

        // Show/hide the left column
        if (_searchPanelColumn != null)
        {
            if (eitherOpen)
            {
                var width = ViewModel?.SearchPanelWidth ?? 350;
                _searchPanelColumn.Width = new GridLength(width);
                _searchPanelColumn.MinWidth = 250;
                _searchPanelColumn.MaxWidth = 600;
            }
            else
            {
                _searchPanelColumn.Width = new GridLength(0);
                _searchPanelColumn.MinWidth = 0;
                _searchPanelColumn.MaxWidth = 0;
            }
        }

        if (_searchSplitterColumn != null)
            _searchSplitterColumn.Width = eitherOpen ? GridLength.Auto : new GridLength(0);
        if (_searchSplitter != null)
            _searchSplitter.IsVisible = eitherOpen;

        // Manage internal row layout
        if (_searchPanelRow != null)
        {
            if (searchOpen)
            {
                _searchPanelRow.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                _searchPanelRow.Height = new GridLength(0);
            }
        }

        if (_explorerSplitterRow != null)
        {
            _explorerSplitterRow.Height = (searchOpen && explorerOpen) ? GridLength.Auto : new GridLength(0);
        }
        if (_leftPanelSplitter != null)
        {
            _leftPanelSplitter.IsVisible = searchOpen && explorerOpen;
        }

        if (_explorerPanelRow != null)
        {
            if (explorerOpen)
            {
                _explorerPanelRow.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                _explorerPanelRow.Height = new GridLength(0);
            }
        }
    }

    // --- Notes Panel ---

    private void InitializeNotesPanel()
    {
        if (ViewModel == null)
            return;

        _notesPanel = this.FindControl<NotesPanel>("NotesPanelControl");
        _notesSplitter = this.FindControl<GridSplitter>("NotesSplitter");
        var contentGrid = _notesPanel?.Parent as Grid;
        if (contentGrid?.ColumnDefinitions.Count >= 5)
        {
            _notesPanelColumn = contentGrid.ColumnDefinitions[4];
            _notesSplitterColumn = contentGrid.ColumnDefinitions[3];
        }
        if (_notesPanel == null)
            return;

        _notesPanelViewModel = new NotesPanelViewModel(ViewModel);
        _notesPanel.DataContext = _notesPanelViewModel;

        // Set initial state
        if (ViewModel.IsNotesPanelOpen)
        {
            ShowNotesPanel();
        }

        // React to ViewModel changes
        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainWindowViewModel.IsNotesPanelOpen))
            {
                if (ViewModel.IsNotesPanelOpen)
                    ShowNotesPanel();
                else
                    HideNotesPanel();
            }
        };

        _notesPanel.CloseRequested += (s, e) =>
        {
            if (ViewModel != null)
                ViewModel.IsNotesPanelOpen = false;
        };

        _notesPanel.NoteOpened += (s, item) =>
        {
            if (item == null || ViewModel == null)
                return;
            var note = ViewModel.NoteService.GetNote(item.Id);
            if (note != null)
                ViewModel.OpenNote(note);
        };
    }

    private void ShowNotesPanel()
    {
        if (_notesPanelColumn == null)
            return;
        var width = ViewModel?.NotesPanelWidth ?? 300;
        _notesPanelColumn.Width = new GridLength(width);
        _notesPanelColumn.MinWidth = 200;
        _notesPanelColumn.MaxWidth = 500;
        if (_notesSplitterColumn != null)
            _notesSplitterColumn.Width = GridLength.Auto;
        if (_notesSplitter != null)
            _notesSplitter.IsVisible = true;
        _notesPanelViewModel?.RefreshTree();
    }

    private void HideNotesPanel()
    {
        if (_notesPanelColumn == null)
            return;
        // Save current width before hiding
        if (ViewModel != null)
            ViewModel.NotesPanelWidth = _notesPanelColumn.Width.Value;
        _notesPanelColumn.Width = new GridLength(0);
        _notesPanelColumn.MinWidth = 0;
        _notesPanelColumn.MaxWidth = 0;
        if (_notesSplitterColumn != null)
            _notesSplitterColumn.Width = new GridLength(0);
        if (_notesSplitter != null)
            _notesSplitter.IsVisible = false;
    }

    private void ToggleNotesPanel()
    {
        if (ViewModel == null)
            return;
        ViewModel.IsNotesPanelOpen = !ViewModel.IsNotesPanelOpen;
    }

    private void SaveAsNote_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ViewModel?.SaveAsNote();
        _notesPanelViewModel?.RefreshTree();
    }

    private void SaveAsNote_Tab_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is TabViewModel tab)
        {
            ViewModel?.SaveAsNote(tab);
            _notesPanelViewModel?.RefreshTree();
        }
    }

    private void Settings_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SettingsDialog();
        dialog.ShowDialog(this);
    }

    private void Replace_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowFindReplaceDialog(replace: true);
    }

    private async void LanguageButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ViewModel?.SelectedTab == null)
            return;

        var syntaxService = App.Instance?.GetSyntaxService();
        if (syntaxService == null)
            return;

        var dialog = new LanguagePickerDialog(syntaxService, ViewModel.SelectedTab.SyntaxName);
        var result = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrEmpty(result))
        {
            ViewModel.SelectedTab.SyntaxName = result;
        }
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        UpdateWindowBoundsOnViewModel();

        // Save search panel width before closing
        if (ViewModel != null && ViewModel.IsSearchPanelOpen && _searchPanelColumn != null)
        {
            ViewModel.SearchPanelWidth = _searchPanelColumn.Width.Value;
        }

        // Save explorer panel width (shared column)
        if (ViewModel != null && (ViewModel.IsExplorerPanelOpen || ViewModel.IsSearchPanelOpen) && _searchPanelColumn != null)
        {
            ViewModel.SearchPanelWidth = _searchPanelColumn.Width.Value;
        }

        // Save notes panel width before closing
        if (ViewModel != null && ViewModel.IsNotesPanelOpen && _notesPanelColumn != null)
        {
            ViewModel.NotesPanelWidth = _notesPanelColumn.Width.Value;
        }

        ViewModel?.FlushAllCaches();
        ViewModel?.SaveState();
        base.OnClosing(e);
    }
}
