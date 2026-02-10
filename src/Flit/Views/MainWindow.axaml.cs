using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flit.Services;
using Flit.ViewModels;

namespace Flit.Views;

public partial class MainWindow : Window
{
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

    public MainWindow()
    {
        InitializeComponent();
        OpenFileCommand = new RelayCommand(_ => RunAsync(OpenFileAsync));
        SaveFileCommand = new RelayCommand(_ => RunAsync(SaveFileAsync));
        SaveAsCommand = new RelayCommand(_ => RunAsync(SaveAsAsync));
        SaveTabCommand = new RelayCommand(tab => RunAsync(() => SaveTabAsync(tab as TabViewModel)));
        SaveTabAsCommand = new RelayCommand(tab => RunAsync(() => SaveTabAsAsync(tab as TabViewModel)));
        RenameTabCommand = new RelayCommand(tab => RunAsync(() => RenameTabAsync(tab as TabViewModel)));
        UndoCommand = new RelayCommand(_ => GetCurrentEditorView()?.Undo());
        RedoCommand = new RelayCommand(_ => GetCurrentEditorView()?.Redo());
        CloseTabWithPromptCommand = new RelayCommand(tab => RunAsync(() => TryCloseTabAsync(tab as TabViewModel)));
        CloseOthersWithPromptCommand = new RelayCommand(tab => RunAsync(() => TryCloseOthersAsync(tab as TabViewModel)));
        CloseToRightWithPromptCommand = new RelayCommand(tab => RunAsync(() => TryCloseToRightAsync(tab as TabViewModel)));
        CloseToLeftWithPromptCommand = new RelayCommand(tab => RunAsync(() => TryCloseToLeftAsync(tab as TabViewModel)));
        CloseAllWithPromptCommand = new RelayCommand(_ => RunAsync(TryCloseAllAsync));
        FindCommand = new RelayCommand(_ => ShowFindReplaceDialog());
        FindInTabsCommand = new RelayCommand(_ => ShowFindInTabsDialog());
        ToggleSearchPanelCommand = new RelayCommand(_ => ToggleSearchPanel());
        ReplaceCommand = new RelayCommand(_ => ShowFindReplaceDialog());

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
        else if (e.Key == Key.Escape && ViewModel?.IsSearchPanelOpen == true && _searchPanel != null)
        {
            // Close search panel if it (or its children) has focus
            if (_searchPanel.IsKeyboardFocusWithin)
            {
                ViewModel.IsSearchPanelOpen = false;
                e.Handled = true;
            }
        }
    }

    private static async void RunAsync(Func<System.Threading.Tasks.Task> asyncAction)
    {
        try
        {
            await asyncAction();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        }
    }

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
            RestoreWindowPosition();
            InitializeSearchPanel();
        }
    }

    private void RestoreWindowPosition()
    {
        if (ViewModel == null) return;

        Width = ViewModel.WindowWidth;
        Height = ViewModel.WindowHeight;

        if (ViewModel.WindowX.HasValue && ViewModel.WindowY.HasValue)
        {
            var x = (int)ViewModel.WindowX.Value;
            var y = (int)ViewModel.WindowY.Value;

            // Validate position is within visible screen area
            if (Screens != null && IsPositionVisible(x, y))
            {
                Position = new PixelPoint(x, y);
            }
            else
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        if (ViewModel.IsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private bool IsPositionVisible(int x, int y)
    {
        if (Screens?.All == null) return false;

        // Check if any part of the window title bar is visible on any screen
        foreach (var screen in Screens.All)
        {
            var bounds = screen.WorkingArea;
            // Consider the window visible if its top-left area overlaps with a screen
            // Allow some tolerance: at least 100px of the window should be on screen
            if (x + 100 > bounds.X && x < bounds.X + bounds.Width &&
                y >= bounds.Y && y < bounds.Y + bounds.Height)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateWindowBoundsOnViewModel()
    {
        if (ViewModel == null) return;

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
        if (!tab.IsDirty)
        {
            // Clean file: auto-reload silently
            tab.ReloadFromDisk();
            return;
        }

        // Dirty file: show dialog
        var dialog = new FileChangedDialog(tab.Title, hasLocalChanges: true);
        var result = await dialog.ShowDialog<FileChangedResult>(this);

        switch (result)
        {
            case FileChangedResult.Reload:
                tab.ReloadFromDisk();
                break;
            case FileChangedResult.KeepChanges:
            case FileChangedResult.Ignore:
                tab.AcknowledgeExternalChanges();
                break;
        }
    }

    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand SaveTabCommand { get; }
    public ICommand SaveTabAsCommand { get; }
    public ICommand RenameTabCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand CloseTabWithPromptCommand { get; }
    public ICommand CloseOthersWithPromptCommand { get; }
    public ICommand CloseToRightWithPromptCommand { get; }
    public ICommand CloseToLeftWithPromptCommand { get; }
    public ICommand CloseAllWithPromptCommand { get; }
    public ICommand FindCommand { get; }
    public ICommand FindInTabsCommand { get; }
    public ICommand ToggleSearchPanelCommand { get; }
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
        if (_draggedTab == null || _draggedElement == null) return;

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
        if (_dragGhost == null) return;

        // Offset the ghost slightly from cursor
        Canvas.SetLeft(_dragGhost, mousePos.X + 10);
        Canvas.SetTop(_dragGhost, mousePos.Y - 10);
    }

    private void UpdateDropIndicator(Point mousePos)
    {
        if (ViewModel == null || _tabStrip == null) return;

        // Find all tab item containers
        var tabItems = GetTabItemContainers();
        if (tabItems.Count == 0) return;

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

        // Adjust for dragged item position
        if (_draggedOriginalIndex >= 0 && newDropIndex > _draggedOriginalIndex)
        {
            // Account for the dragged item being "removed"
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
        if (_tabStrip == null) return result;

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

    private async System.Threading.Tasks.Task SaveFileAsync()
    {
        var tab = ViewModel?.SelectedTab;
        if (tab == null) return;

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            await SaveAsAsync();
            return;
        }

        ViewModel?.SaveFile(tab);
    }

    private async System.Threading.Tasks.Task SaveAsAsync()
    {
        var tab = ViewModel?.SelectedTab;
        if (tab == null) return;

        await SaveTabAsAsync(tab);
    }

    private async System.Threading.Tasks.Task SaveTabAsync(TabViewModel? tab)
    {
        if (tab == null) return;

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            await SaveTabAsAsync(tab);
            return;
        }

        ViewModel?.SaveFile(tab);
    }

    private async System.Threading.Tasks.Task SaveTabAsAsync(TabViewModel? tab)
    {
        if (tab == null) return;

        var suggestedName = tab.Title;
        if (string.IsNullOrEmpty(System.IO.Path.GetExtension(suggestedName)))
        {
            suggestedName += ".txt";
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save File",
            SuggestedFileName = suggestedName,
            DefaultExtension = "txt",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                FilePickerFileTypes.All
            }
        });

        if (file != null)
        {
            var path = file.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                ViewModel?.SaveFile(tab, path);
            }
        }
    }

    private async System.Threading.Tasks.Task RenameTabAsync(TabViewModel? tab)
    {
        if (tab == null) return;

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

    private void Redo_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        GetCurrentEditorView()?.Redo();
    }

    public async System.Threading.Tasks.Task<bool> TryCloseTabAsync(TabViewModel? tab)
    {
        if (tab == null) return true;

        if (tab.IsDirty)
        {
            var dialog = new UnsavedChangesDialog(tab.Title);
            var result = await dialog.ShowDialog<UnsavedChangesResult>(this);

            switch (result)
            {
                case UnsavedChangesResult.Save:
                    if (string.IsNullOrEmpty(tab.FilePath))
                    {
                        await SaveTabAsAsync(tab);
                        if (tab.IsDirty) return false; // Save was cancelled
                    }
                    else
                    {
                        ViewModel?.SaveFile(tab);
                    }
                    break;
                case UnsavedChangesResult.Cancel:
                    return false;
                case UnsavedChangesResult.DontSave:
                    break;
            }
        }

        ViewModel?.CloseTab(tab);
        return true;
    }

    public async System.Threading.Tasks.Task<bool> TryCloseAllDirtyTabsAsync()
    {
        if (ViewModel == null) return true;

        var dirtyTabs = ViewModel.Tabs.Where(t => t.IsDirty).ToList();
        foreach (var tab in dirtyTabs)
        {
            var dialog = new UnsavedChangesDialog(tab.Title);
            var result = await dialog.ShowDialog<UnsavedChangesResult>(this);

            switch (result)
            {
                case UnsavedChangesResult.Save:
                    if (string.IsNullOrEmpty(tab.FilePath))
                    {
                        await SaveTabAsAsync(tab);
                        if (tab.IsDirty) return false;
                    }
                    else
                    {
                        ViewModel.SaveFile(tab);
                    }
                    break;
                case UnsavedChangesResult.Cancel:
                    return false;
                case UnsavedChangesResult.DontSave:
                    break;
            }
        }

        return true;
    }

    private async System.Threading.Tasks.Task TryCloseOthersAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null) return;

        var tabsToClose = ViewModel.Tabs.Where(t => t != tab).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseToRightAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null) return;

        var index = ViewModel.Tabs.IndexOf(tab);
        if (index < 0) return;

        var tabsToClose = ViewModel.Tabs.Skip(index + 1).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseToLeftAsync(TabViewModel? tab)
    {
        if (tab == null || ViewModel == null) return;

        var index = ViewModel.Tabs.IndexOf(tab);
        if (index <= 0) return;

        var tabsToClose = ViewModel.Tabs.Take(index).ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private async System.Threading.Tasks.Task TryCloseAllAsync()
    {
        if (ViewModel == null) return;

        var tabsToClose = ViewModel.Tabs.ToList();
        foreach (var t in tabsToClose)
        {
            if (!await TryCloseTabAsync(t))
                return; // User cancelled
        }
    }

    private void ShowFindReplaceDialog()
    {
        var editorView = GetCurrentEditorView();
        var editor = editorView?.GetEditor();
        if (editor == null) return;

        var dialog = new FindReplaceDialog(editor);
        dialog.Show(this);
    }

    private void ShowFindInTabsDialog()
    {
        if (ViewModel == null) return;

        var dialog = new FindInTabsDialog(ViewModel, GoToSearchResult);
        dialog.Show(this);
    }

    private void GoToSearchResult(TabViewModel tab, int offset, int length, int line)
    {
        if (ViewModel == null) return;

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
        ShowFindReplaceDialog();
    }

    private void ToggleSearchPanel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ToggleSearchPanel();
    }

    private void ToggleSearchPanel()
    {
        if (ViewModel == null) return;

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
        if (ViewModel == null) return;

        _searchPanel = this.FindControl<SearchPanel>("SearchPanelControl");
        _searchSplitter = this.FindControl<GridSplitter>("SearchSplitter");
        // ColumnDefinitions aren't Controls, so find them via the parent Grid
        var contentGrid = _searchPanel?.Parent as Grid;
        if (contentGrid?.ColumnDefinitions.Count >= 2)
        {
            _searchPanelColumn = contentGrid.ColumnDefinitions[0];
            _searchSplitterColumn = contentGrid.ColumnDefinitions[1];
        }
        if (_searchPanel == null) return;

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
            if (item == null) return;
            NavigateToSearchResult(item);
        };
    }

    private void ShowSearchPanel()
    {
        if (_searchPanelColumn == null) return;
        var width = ViewModel?.SearchPanelWidth ?? 350;
        _searchPanelColumn.Width = new GridLength(width);
        _searchPanelColumn.MinWidth = 250;
        _searchPanelColumn.MaxWidth = 600;
        if (_searchSplitterColumn != null)
            _searchSplitterColumn.Width = GridLength.Auto;
        if (_searchSplitter != null)
            _searchSplitter.IsVisible = true;
    }

    private void HideSearchPanel()
    {
        if (_searchPanelColumn == null) return;
        // Save current width before hiding
        if (ViewModel != null)
            ViewModel.SearchPanelWidth = _searchPanelColumn.Width.Value;
        _searchPanelColumn.Width = new GridLength(0);
        _searchPanelColumn.MinWidth = 0;
        _searchPanelColumn.MaxWidth = 0;
        if (_searchSplitterColumn != null)
            _searchSplitterColumn.Width = new GridLength(0);
        if (_searchSplitter != null)
            _searchSplitter.IsVisible = false;
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
        if (ViewModel == null) return;

        if (item.Tab != null)
        {
            // Tab mode: switch to the tab and navigate
            GoToSearchResult(item.Tab, item.StartOffset, item.Length, item.LineNumber);
        }
        else if (!string.IsNullOrEmpty(item.FilePath))
        {
            // File mode: open file (or switch to existing tab) and navigate
            var tab = ViewModel.OpenFile(item.FilePath);
            GoToSearchResult(tab, item.StartOffset, item.Length, item.LineNumber);
        }
    }

    private void Replace_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ShowFindReplaceDialog();
    }

    private async void LanguageButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel?.SelectedTab == null) return;

        var syntaxService = App.Instance?.GetSyntaxService();
        if (syntaxService == null) return;

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

        ViewModel?.SaveState();
        base.OnClosing(e);
    }
}
