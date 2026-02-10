using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Flit.Models;
using Flit.Services;

namespace Flit.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly StateService _stateService;
    private readonly CacheService _cacheService;
    private readonly SyntaxService _syntaxService;
    private readonly FileChangeService _fileChangeService;
    private TabViewModel? _selectedTab;
    private double _fontSize = 10;
    private bool _showWhitespace = false;
    private bool _showLineNumbers = true;
    private bool _useLightTheme = false;
    private bool _isSearchPanelOpen;
    private double _searchPanelWidth = 350;
    private StatusBarViewModel _statusBar = new();

    // Window position/size state
    public double WindowWidth { get; set; } = 1200;
    public double WindowHeight { get; set; } = 800;
    public double? WindowX { get; set; }
    public double? WindowY { get; set; }
    public bool IsMaximized { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<TabViewModel>? ExternalChangeDetected;
    public event EventHandler<bool>? ThemeChanged;

    public MainWindowViewModel(StateService stateService, CacheService cacheService, SyntaxService syntaxService, FileChangeService fileChangeService)
    {
        _stateService = stateService;
        _cacheService = cacheService;
        _syntaxService = syntaxService;
        _fileChangeService = fileChangeService;

        Tabs = new ObservableCollection<TabViewModel>();

        NewTabCommand = new RelayCommand(_ => NewTab());
        CloseTabCommand = new RelayCommand(tab => CloseTab(tab as TabViewModel));
        CloseOthersCommand = new RelayCommand(tab => CloseOthers(tab as TabViewModel));
        CloseToRightCommand = new RelayCommand(tab => CloseToRight(tab as TabViewModel));
        CloseToLeftCommand = new RelayCommand(tab => CloseToLeft(tab as TabViewModel));
        CloseUnchangedCommand = new RelayCommand(_ => CloseUnchanged());
        CloseAllCommand = new RelayCommand(_ => CloseAll());
        SaveAllCommand = new RelayCommand(_ => SaveAll());
        ToggleWhitespaceCommand = new RelayCommand(_ => ShowWhitespace = !ShowWhitespace);
        ToggleLineNumbersCommand = new RelayCommand(_ => ShowLineNumbers = !ShowLineNumbers);
        ToggleLightThemeCommand = new RelayCommand(_ => UseLightTheme = !UseLightTheme);
        ToggleSearchPanelCommand = new RelayCommand(_ => IsSearchPanelOpen = !IsSearchPanelOpen);

        LoadState();

        // Ensure at least one tab exists
        if (Tabs.Count == 0)
        {
            NewTab();
        }

        // Setup file change monitoring
        _fileChangeService.FileChangedExternally += OnFileChangedExternally;
        _fileChangeService.SetTabs(Tabs);
        _fileChangeService.Start();
    }

    private void OnFileChangedExternally(object? sender, TabViewModel tab)
    {
        ExternalChangeDetected?.Invoke(this, tab);
    }

    public ObservableCollection<TabViewModel> Tabs { get; }

    public TabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (_selectedTab != value)
            {
                _selectedTab = value;
                OnPropertyChanged();
            }
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (Math.Abs(_fontSize - value) > 0.01)
            {
                _fontSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ZoomPercentage));
                SaveState();
            }
        }
    }

    public int ZoomPercentage
    {
        get => (int)Math.Round((_fontSize / 10.0) * 100);
        set
        {
            var newFontSize = Math.Clamp((value / 100.0) * 10, 6, 72);
            FontSize = newFontSize;
        }
    }

    public bool ShowWhitespace
    {
        get => _showWhitespace;
        set
        {
            if (_showWhitespace != value)
            {
                _showWhitespace = value;
                OnPropertyChanged();
                SaveState();
            }
        }
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (_showLineNumbers != value)
            {
                _showLineNumbers = value;
                OnPropertyChanged();
                SaveState();
            }
        }
    }

    public bool UseLightTheme
    {
        get => _useLightTheme;
        set
        {
            if (_useLightTheme != value)
            {
                _useLightTheme = value;
                OnPropertyChanged();
                ThemeChanged?.Invoke(this, value);
                RefreshAllSyntaxHighlighting();
                SaveState();
            }
        }
    }

    public bool IsSearchPanelOpen
    {
        get => _isSearchPanelOpen;
        set
        {
            if (_isSearchPanelOpen != value)
            {
                _isSearchPanelOpen = value;
                OnPropertyChanged();
                SaveState();
            }
        }
    }

    public double SearchPanelWidth
    {
        get => _searchPanelWidth;
        set
        {
            if (Math.Abs(_searchPanelWidth - value) > 0.5)
            {
                _searchPanelWidth = value;
                OnPropertyChanged();
            }
        }
    }

    public ICommand NewTabCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand CloseOthersCommand { get; }
    public ICommand CloseToRightCommand { get; }
    public ICommand CloseToLeftCommand { get; }
    public ICommand CloseUnchangedCommand { get; }
    public ICommand CloseAllCommand { get; }
    public ICommand SaveAllCommand { get; }
    public ICommand ToggleWhitespaceCommand { get; }
    public ICommand ToggleLineNumbersCommand { get; }
    public ICommand ToggleLightThemeCommand { get; }
    public ICommand ToggleSearchPanelCommand { get; }

    public StatusBarViewModel StatusBar => _statusBar;

    public void NewTab()
    {
        var tab = new TabViewModel(_cacheService, _syntaxService)
        {
            Title = GetNextNoteName()
        };
        Tabs.Add(tab);
        SelectedTab = tab;
        SaveState();
    }

    private string GetNextNoteName()
    {
        var usedNumbers = Tabs
            .Where(t => t.Title.StartsWith("note", StringComparison.OrdinalIgnoreCase))
            .Select(t =>
            {
                var suffix = t.Title.Substring(4);
                return int.TryParse(suffix, out var num) ? num : 0;
            })
            .Where(n => n > 0)
            .ToHashSet();

        int next = 1;
        while (usedNumbers.Contains(next))
        {
            next++;
        }

        return $"note{next}";
    }

    public TabViewModel OpenFile(string filePath)
    {
        // Check if file is already open
        var existingTab = Tabs.FirstOrDefault(t =>
            !string.IsNullOrEmpty(t.FilePath) &&
            t.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        if (existingTab != null)
        {
            SelectedTab = existingTab;
            return existingTab;
        }

        var tab = new TabViewModel(_cacheService, _syntaxService)
        {
            FilePath = filePath,
            Title = Path.GetFileName(filePath)
        };

        try
        {
            var content = File.ReadAllText(filePath);
            tab.SetOriginalContent(content);
            tab.LastKnownModified = File.GetLastWriteTimeUtc(filePath);
        }
        catch (Exception)
        {
            // If file can't be read, leave content empty
        }

        Tabs.Add(tab);
        SelectedTab = tab;
        SaveState();

        return tab;
    }

    public void SaveFile(TabViewModel tab, string? filePath = null)
    {
        if (filePath != null)
        {
            tab.FilePath = filePath;
            tab.Title = Path.GetFileName(filePath);
        }

        if (string.IsNullOrEmpty(tab.FilePath))
        {
            return;
        }

        try
        {
            File.WriteAllText(tab.FilePath, tab.Content);
            tab.LastKnownModified = File.GetLastWriteTimeUtc(tab.FilePath);
            tab.MarkAsSaved();
            SaveState();
        }
        catch (Exception)
        {
            // Handle save error (could show dialog in view)
        }
    }

    public void CloseTab(TabViewModel? tab)
    {
        if (tab == null) return;

        var index = Tabs.IndexOf(tab);
        Tabs.Remove(tab);

        // Select adjacent tab
        if (Tabs.Count > 0)
        {
            SelectedTab = Tabs[Math.Min(index, Tabs.Count - 1)];
        }
        else
        {
            SelectedTab = null;
        }

        // Delete cache for closed tab
        _cacheService.DeleteCache(tab.Id);

        SaveState();
    }

    public void MoveTab(TabViewModel sourceTab, TabViewModel targetTab)
    {
        var sourceIndex = Tabs.IndexOf(sourceTab);
        var targetIndex = Tabs.IndexOf(targetTab);

        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
            return;

        Tabs.Move(sourceIndex, targetIndex);
        SaveState();
    }

    public void CloseOthers(TabViewModel? tab)
    {
        if (tab == null) return;

        var tabsToClose = Tabs.Where(t => t != tab).ToList();
        foreach (var t in tabsToClose)
        {
            _cacheService.DeleteCache(t.Id);
            Tabs.Remove(t);
        }

        SelectedTab = tab;
        SaveState();
    }

    public void CloseToRight(TabViewModel? tab)
    {
        if (tab == null) return;

        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        var tabsToClose = Tabs.Skip(index + 1).ToList();
        foreach (var t in tabsToClose)
        {
            _cacheService.DeleteCache(t.Id);
            Tabs.Remove(t);
        }

        SaveState();
    }

    public void CloseToLeft(TabViewModel? tab)
    {
        if (tab == null) return;

        var index = Tabs.IndexOf(tab);
        if (index <= 0) return;

        var tabsToClose = Tabs.Take(index).ToList();
        foreach (var t in tabsToClose)
        {
            _cacheService.DeleteCache(t.Id);
            Tabs.Remove(t);
        }

        SelectedTab = tab;
        SaveState();
    }

    public void CloseUnchanged()
    {
        var tabsToClose = Tabs.Where(t => !t.IsDirty).ToList();
        var currentSelected = SelectedTab;

        foreach (var t in tabsToClose)
        {
            _cacheService.DeleteCache(t.Id);
            Tabs.Remove(t);
        }

        // Try to keep current selection, or select first remaining tab
        if (currentSelected != null && Tabs.Contains(currentSelected))
        {
            SelectedTab = currentSelected;
        }
        else if (Tabs.Count > 0)
        {
            SelectedTab = Tabs[0];
        }
        else
        {
            SelectedTab = null;
        }

        SaveState();
    }

    public void RenameTab(TabViewModel tab, string newTitle)
    {
        tab.Title = newTitle;
        SaveState();
    }

    public void CloseAll()
    {
        var tabsToClose = Tabs.ToList();
        foreach (var tab in tabsToClose)
        {
            _cacheService.DeleteCache(tab.Id);
            Tabs.Remove(tab);
        }

        SelectedTab = null;
        SaveState();
    }

    public void SaveAll()
    {
        foreach (var tab in Tabs)
        {
            if (tab.IsDirty && !string.IsNullOrEmpty(tab.FilePath))
            {
                try
                {
                    File.WriteAllText(tab.FilePath, tab.Content);
                    tab.LastKnownModified = File.GetLastWriteTimeUtc(tab.FilePath);
                    tab.MarkAsSaved();
                }
                catch (Exception)
                {
                    // Skip tabs that fail to save
                }
            }
        }

        SaveState();
    }

    private void RefreshAllSyntaxHighlighting()
    {
        foreach (var tab in Tabs)
        {
            tab.RefreshSyntaxHighlighting();
        }
    }

    public void SaveState()
    {
        var state = new AppState
        {
            Tabs = Tabs.Select((t, i) => t.ToState(i)).ToList(),
            ActiveTabId = SelectedTab?.Id,
            FontSize = _fontSize,
            ShowWhitespace = _showWhitespace,
            ShowLineNumbers = _showLineNumbers,
            UseLightTheme = _useLightTheme,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowX = WindowX,
            WindowY = WindowY,
            IsMaximized = IsMaximized,
            SearchPanelOpen = _isSearchPanelOpen,
            SearchPanelWidth = _searchPanelWidth
        };

        _stateService.SaveState(state);
    }

    private void LoadState()
    {
        var state = _stateService.LoadState();

        _fontSize = state.FontSize > 0 ? state.FontSize : 10;
        _showWhitespace = state.ShowWhitespace;
        _showLineNumbers = state.ShowLineNumbers;
        _useLightTheme = state.UseLightTheme;
        WindowWidth = state.WindowWidth > 0 ? state.WindowWidth : 1200;
        WindowHeight = state.WindowHeight > 0 ? state.WindowHeight : 800;
        WindowX = state.WindowX;
        WindowY = state.WindowY;
        IsMaximized = state.IsMaximized;
        _isSearchPanelOpen = state.SearchPanelOpen;
        _searchPanelWidth = state.SearchPanelWidth > 0 ? state.SearchPanelWidth : 350;

        foreach (var tabState in state.Tabs.OrderBy(t => t.Order))
        {
            var tab = TabViewModel.FromState(tabState, _cacheService, _syntaxService);
            Tabs.Add(tab);
        }

        if (state.ActiveTabId.HasValue)
        {
            SelectedTab = Tabs.FirstOrDefault(t => t.Id == state.ActiveTabId.Value);
        }

        if (SelectedTab == null && Tabs.Count > 0)
        {
            SelectedTab = Tabs[0];
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<object?, bool>? _canExecute;

    public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
