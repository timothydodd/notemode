using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Flit.ViewModels;

public class SearchResultGroup : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string Name { get; init; } = "";
    public string? FilePath { get; init; }
    public TabViewModel? Tab { get; init; }
    public int MatchCount { get; init; }
    public ObservableCollection<SearchResultItem> Items { get; init; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class SearchResultItem
{
    public TabViewModel? Tab { get; init; }
    public string? FilePath { get; init; }
    public int LineNumber { get; init; }
    public int ColumnNumber { get; init; }
    public string LinePreview { get; init; } = "";
    public string PreviewBefore { get; init; } = "";
    public string MatchText { get; init; } = "";
    public string PreviewAfter { get; init; } = "";
    public int StartOffset { get; init; }
    public int Length { get; init; }
}

public class SearchPanelViewModel : INotifyPropertyChanged
{
    private string _searchText = "";
    private bool _isCaseSensitive;
    private bool _isWholeWord;
    private bool _isTabsMode = true;
    private string? _folderPath;
    private string? _folderDisplayName;
    private string _statusText = "";
    private bool _isSearching;
    private DispatcherTimer? _debounceTimer;
    private CancellationTokenSource? _fileCts;

    private readonly MainWindowViewModel _mainViewModel;

    public SearchPanelViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;

        _debounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _debounceTimer.Tick += (s, e) =>
        {
            _debounceTimer.Stop();
            PerformSearch();
        };
    }

    public ObservableCollection<SearchResultGroup> Results { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText != value)
            {
                _searchText = value;
                OnPropertyChanged();
                DebouncedSearch();
            }
        }
    }

    public bool IsCaseSensitive
    {
        get => _isCaseSensitive;
        set
        {
            if (_isCaseSensitive != value)
            {
                _isCaseSensitive = value;
                OnPropertyChanged();
                PerformSearch();
            }
        }
    }

    public bool IsWholeWord
    {
        get => _isWholeWord;
        set
        {
            if (_isWholeWord != value)
            {
                _isWholeWord = value;
                OnPropertyChanged();
                PerformSearch();
            }
        }
    }

    public bool IsTabsMode
    {
        get => _isTabsMode;
        set
        {
            if (_isTabsMode != value)
            {
                _isTabsMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsFilesMode));
                PerformSearch();
            }
        }
    }

    public bool IsFilesMode
    {
        get => !_isTabsMode;
        set
        {
            IsTabsMode = !value;
        }
    }

    public string? FolderPath
    {
        get => _folderPath;
        set
        {
            if (_folderPath != value)
            {
                _folderPath = value;
                OnPropertyChanged();
                UpdateFolderDisplayName();
                PerformSearch();
            }
        }
    }

    public string? FolderDisplayName
    {
        get => _folderDisplayName;
        private set
        {
            if (_folderDisplayName != value)
            {
                _folderDisplayName = value;
                OnPropertyChanged();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        set
        {
            if (_isSearching != value)
            {
                _isSearching = value;
                OnPropertyChanged();
            }
        }
    }

    private void UpdateFolderDisplayName()
    {
        if (string.IsNullOrEmpty(_folderPath))
        {
            FolderDisplayName = null;
            return;
        }

        var name = Path.GetFileName(_folderPath);
        if (string.IsNullOrEmpty(name))
            name = _folderPath;

        FolderDisplayName = name;
    }

    private void DebouncedSearch()
    {
        _debounceTimer?.Stop();
        _debounceTimer?.Start();
    }

    public void PerformSearch()
    {
        _debounceTimer?.Stop();

        if (IsTabsMode)
        {
            SearchInTabs();
        }
        else
        {
            _ = SearchInFilesAsync();
        }
    }

    private void SearchInTabs()
    {
        _fileCts?.Cancel();
        Results.Clear();

        if (string.IsNullOrEmpty(_searchText) || _searchText.Length < 2)
        {
            StatusText = "";
            return;
        }

        var comparison = _isCaseSensitive
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        int totalMatches = 0;
        int tabsWithMatches = 0;

        foreach (var tab in _mainViewModel.Tabs)
        {
            var content = tab.Content ?? "";
            var lines = content.Split('\n');
            int offset = 0;
            var items = new ObservableCollection<SearchResultItem>();

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                int columnIndex = 0;

                while ((columnIndex = FindMatch(line, _searchText, columnIndex, comparison)) != -1)
                {
                    var matchLen = _searchText.Length;
                    var trimmedLine = line.TrimEnd('\r');
                    var preview = trimmedLine.Length > 200 ? trimmedLine.Substring(0, 200) : trimmedLine;

                    // Calculate preview parts for highlighting
                    var previewCol = Math.Min(columnIndex, preview.Length);
                    var previewMatchEnd = Math.Min(columnIndex + matchLen, preview.Length);
                    var before = preview.Substring(0, previewCol);
                    var match = preview.Substring(previewCol, previewMatchEnd - previewCol);
                    var after = preview.Substring(previewMatchEnd);

                    items.Add(new SearchResultItem
                    {
                        Tab = tab,
                        LineNumber = lineIndex + 1,
                        ColumnNumber = columnIndex + 1,
                        LinePreview = preview,
                        PreviewBefore = before,
                        MatchText = match,
                        PreviewAfter = after,
                        StartOffset = offset + columnIndex,
                        Length = matchLen
                    });

                    totalMatches++;
                    columnIndex += matchLen;
                }

                offset += line.Length + 1;
            }

            if (items.Count > 0)
            {
                tabsWithMatches++;
                Results.Add(new SearchResultGroup
                {
                    Name = tab.Title,
                    Tab = tab,
                    MatchCount = items.Count,
                    Items = items
                });
            }
        }

        StatusText = totalMatches == 0
            ? "No matches found"
            : $"{totalMatches} match{(totalMatches == 1 ? "" : "es")} in {tabsWithMatches} tab{(tabsWithMatches == 1 ? "" : "s")}";
    }

    private async Task SearchInFilesAsync()
    {
        _fileCts?.Cancel();

        Results.Clear();

        if (string.IsNullOrEmpty(_searchText) || _searchText.Length < 2 || string.IsNullOrEmpty(_folderPath))
        {
            StatusText = string.IsNullOrEmpty(_folderPath) ? "Select a folder to search" : "";
            return;
        }

        if (!Directory.Exists(_folderPath))
        {
            StatusText = "Folder not found";
            return;
        }

        _fileCts = new CancellationTokenSource();
        var token = _fileCts.Token;
        IsSearching = true;

        try
        {
            var comparison = _isCaseSensitive
                ? StringComparison.Ordinal
                : StringComparison.OrdinalIgnoreCase;

            int totalMatches = 0;
            int filesWithMatches = 0;
            int filesScanned = 0;
            const int maxFiles = 5000;
            const long maxFileSize = 1024 * 1024; // 1MB

            var skipDirs = new[] { ".git", ".vs", "node_modules", "bin", "obj", ".svn", ".hg" };

            var files = EnumerateFilesFiltered(_folderPath, skipDirs, maxFiles);

            foreach (var filePath in files)
            {
                token.ThrowIfCancellationRequested();
                filesScanned++;

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length > maxFileSize) continue;

                    // Skip likely binary files
                    if (IsBinaryExtension(fileInfo.Extension)) continue;

                    var content = await File.ReadAllTextAsync(filePath, token);
                    var lines = content.Split('\n');
                    int offset = 0;
                    var items = new ObservableCollection<SearchResultItem>();

                    for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                    {
                        var line = lines[lineIndex];
                        int columnIndex = 0;

                        while ((columnIndex = FindMatch(line, _searchText, columnIndex, comparison)) != -1)
                        {
                            var matchLen = _searchText.Length;
                            var trimmedLine = line.TrimEnd('\r');
                            var preview = trimmedLine.Length > 200 ? trimmedLine.Substring(0, 200) : trimmedLine;

                            var previewCol = Math.Min(columnIndex, preview.Length);
                            var previewMatchEnd = Math.Min(columnIndex + matchLen, preview.Length);
                            var before = preview.Substring(0, previewCol);
                            var match = preview.Substring(previewCol, previewMatchEnd - previewCol);
                            var after = preview.Substring(previewMatchEnd);

                            items.Add(new SearchResultItem
                            {
                                FilePath = filePath,
                                LineNumber = lineIndex + 1,
                                ColumnNumber = columnIndex + 1,
                                LinePreview = preview,
                                PreviewBefore = before,
                                MatchText = match,
                                PreviewAfter = after,
                                StartOffset = offset + columnIndex,
                                Length = matchLen
                            });

                            totalMatches++;
                            columnIndex += matchLen;
                        }

                        offset += line.Length + 1;
                    }

                    if (items.Count > 0)
                    {
                        filesWithMatches++;
                        var relativePath = Path.GetRelativePath(_folderPath, filePath);

                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Results.Add(new SearchResultGroup
                            {
                                Name = relativePath,
                                FilePath = filePath,
                                MatchCount = items.Count,
                                Items = items
                            });
                        });
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch
                {
                    // Skip files that can't be read
                }
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                StatusText = totalMatches == 0
                    ? "No matches found"
                    : $"{totalMatches} match{(totalMatches == 1 ? "" : "es")} in {filesWithMatches} file{(filesWithMatches == 1 ? "" : "s")}";
            });
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled
        }
        finally
        {
            IsSearching = false;
        }
    }

    private int FindMatch(string line, string searchText, int startIndex, StringComparison comparison)
    {
        var idx = line.IndexOf(searchText, startIndex, comparison);
        if (idx == -1) return -1;

        if (_isWholeWord)
        {
            while (idx != -1)
            {
                bool startOk = idx == 0 || !char.IsLetterOrDigit(line[idx - 1]);
                bool endOk = idx + searchText.Length >= line.Length || !char.IsLetterOrDigit(line[idx + searchText.Length]);

                if (startOk && endOk)
                    return idx;

                idx = line.IndexOf(searchText, idx + 1, comparison);
            }
            return -1;
        }

        return idx;
    }

    private static System.Collections.Generic.IEnumerable<string> EnumerateFilesFiltered(
        string rootPath, string[] skipDirs, int maxFiles)
    {
        int count = 0;
        var stack = new System.Collections.Generic.Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0 && count < maxFiles)
        {
            var dir = stack.Pop();

            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (count >= maxFiles) yield break;
                count++;
                yield return file;
            }

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                if (!skipDirs.Contains(dirName, StringComparer.OrdinalIgnoreCase))
                {
                    stack.Push(subdir);
                }
            }
        }
    }

    private static bool IsBinaryExtension(string ext)
    {
        var binaryExts = new[]
        {
            ".exe", ".dll", ".pdb", ".obj", ".bin", ".zip", ".tar", ".gz", ".7z",
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg",
            ".mp3", ".mp4", ".avi", ".mov", ".wav",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx",
            ".nupkg", ".snk", ".pfx", ".woff", ".woff2", ".ttf", ".eot"
        };
        return binaryExts.Contains(ext.ToLowerInvariant());
    }

    public void Clear()
    {
        _fileCts?.Cancel();
        SearchText = "";
        Results.Clear();
        StatusText = "";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
