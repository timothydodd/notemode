using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace NoteMode.ViewModels;

public class ExplorerPanelViewModel : INotifyPropertyChanged
{
    private readonly MainWindowViewModel _mainViewModel;
    private string? _currentRootPath;
    private string? _rootDisplayName;
    private FileSystemWatcher? _watcher;

    public ObservableCollection<FileTreeItemViewModel> RootItems { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExplorerPanelViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public string? CurrentRootPath
    {
        get => _currentRootPath;
        private set
        {
            if (_currentRootPath != value)
            {
                _currentRootPath = value;
                OnPropertyChanged();
            }
        }
    }

    public string? RootDisplayName
    {
        get => _rootDisplayName;
        private set
        {
            if (_rootDisplayName != value)
            {
                _rootDisplayName = value;
                OnPropertyChanged();
            }
        }
    }

    public void UpdateRoot(string? filePath)
    {
        string? directory = null;

        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            directory = Path.GetDirectoryName(filePath);
        }
        else if (!string.IsNullOrEmpty(filePath) && Directory.Exists(filePath))
        {
            directory = filePath;
        }

        if (string.Equals(directory, _currentRootPath, StringComparison.OrdinalIgnoreCase))
            return;

        CurrentRootPath = directory;
        RootDisplayName = directory != null ? Path.GetFileName(directory) : null;

        // If the folder name is empty (e.g., drive root), use the full path
        if (string.IsNullOrEmpty(RootDisplayName) && directory != null)
            RootDisplayName = directory;

        RebuildTree();
        SetupWatcher();
    }

    private void RebuildTree()
    {
        RootItems.Clear();

        if (string.IsNullOrEmpty(_currentRootPath) || !Directory.Exists(_currentRootPath))
            return;

        try
        {
            var items = LoadDirectoryContents(_currentRootPath);
            foreach (var item in items)
            {
                WireExpandHandler(item);
                RootItems.Add(item);
            }
        }
        catch (Exception)
        {
            // Permission denied or other IO error
        }
    }

    private static FileTreeItemViewModel[] LoadDirectoryContents(string directoryPath)
    {
        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            var dirs = dirInfo.GetDirectories()
                .Where(d => !d.Name.StartsWith('.') && !IsSkippedDirectory(d.Name))
                .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
                .Select(d => new FileTreeItemViewModel(d.Name, d.FullName, true));

            var files = dirInfo.GetFiles()
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => new FileTreeItemViewModel(f.Name, f.FullName, false));

            return dirs.Concat(files).ToArray();
        }
        catch (Exception)
        {
            return Array.Empty<FileTreeItemViewModel>();
        }
    }

    private static bool IsSkippedDirectory(string name)
    {
        return name is "node_modules" or "bin" or "obj" or ".git" or ".vs" or ".svn" or ".hg";
    }

    public void LoadChildren(FileTreeItemViewModel item)
    {
        if (!item.IsDirectory || item.ChildrenLoaded)
            return;

        Task.Run(() =>
        {
            var children = LoadDirectoryContents(item.FullPath);

            Dispatcher.UIThread.Post(() =>
            {
                item.Children.Clear();
                foreach (var child in children)
                {
                    WireExpandHandler(child);
                    item.Children.Add(child);
                }
                item.ChildrenLoaded = true;
            });
        });
    }

    private void WireExpandHandler(FileTreeItemViewModel item)
    {
        if (item.IsDirectory)
        {
            item.ExpandRequested += (s, e) =>
            {
                if (s is FileTreeItemViewModel expandedItem)
                    LoadChildren(expandedItem);
            };
        }
    }

    private void SetupWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;

        if (string.IsNullOrEmpty(_currentRootPath) || !Directory.Exists(_currentRootPath))
            return;

        try
        {
            _watcher = new FileSystemWatcher(_currentRootPath)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnFileSystemChanged;
            _watcher.Deleted += OnFileSystemChanged;
            _watcher.Renamed += OnFileSystemChanged;
        }
        catch (Exception)
        {
            // Watcher setup failed, proceed without live updates
        }
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(RebuildTree);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
