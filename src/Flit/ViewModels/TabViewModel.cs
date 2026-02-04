using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Timers;
using AvaloniaEdit.Highlighting;
using Flit.Models;
using Flit.Services;

namespace Flit.ViewModels;

public class TabViewModel : INotifyPropertyChanged
{
    private readonly CacheService _cacheService;
    private readonly SyntaxService _syntaxService;
    private readonly System.Timers.Timer _cacheTimer;

    private string _title = "Untitled";
    private string? _filePath;
    private string _content = string.Empty;
    private bool _isDirty;
    private IHighlightingDefinition? _syntaxHighlighting;
    private string _syntaxName = "Plain Text";
    private string _originalContent = string.Empty;
    private bool _isContentLoaded;
    private bool _hasCachedChanges;
    private DateTime? _lastKnownModified;
    private bool _hasExternalChanges;
    private bool _externalChangesAcknowledged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public TabViewModel(CacheService cacheService, SyntaxService syntaxService)
    {
        _cacheService = cacheService;
        _syntaxService = syntaxService;
        Id = Guid.NewGuid();
        _isContentLoaded = true; // New tabs start with empty content, already "loaded"

        _cacheTimer = new System.Timers.Timer(500);
        _cacheTimer.AutoReset = false;
        _cacheTimer.Elapsed += OnCacheTimerElapsed;
    }

    public Guid Id { get; set; }

    public string Title
    {
        get => _title;
        set
        {
            if (_title != value)
            {
                _title = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public string DisplayTitle
    {
        get
        {
            var title = Title;
            if (HasExternalChanges)
                title = "⚠ " + title;
            if (IsDirty || HasCachedChanges)
                title = "● " + title;
            return title;
        }
    }

    public string? FilePath
    {
        get => _filePath;
        set
        {
            if (_filePath != value)
            {
                _filePath = value;
                OnPropertyChanged();
                UpdateSyntaxHighlighting();
                if (!string.IsNullOrEmpty(value))
                {
                    Title = Path.GetFileName(value);
                }
            }
        }
    }

    public string Content
    {
        get
        {
            EnsureContentLoaded();
            return _content;
        }
        set
        {
            EnsureContentLoaded();
            if (_content != value)
            {
                _content = value;
                OnPropertyChanged();
                UpdateDirtyState();
                ScheduleCacheSave();
            }
        }
    }

    public bool HasCachedChanges
    {
        get => _hasCachedChanges;
        private set
        {
            if (_hasCachedChanges != value)
            {
                _hasCachedChanges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (_isDirty != value)
            {
                _isDirty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public IHighlightingDefinition? SyntaxHighlighting
    {
        get => _syntaxHighlighting;
        private set
        {
            if (_syntaxHighlighting != value)
            {
                _syntaxHighlighting = value;
                OnPropertyChanged();
            }
        }
    }

    public string SyntaxName
    {
        get => _syntaxName;
        set
        {
            if (_syntaxName != value)
            {
                _syntaxName = value;
                SyntaxHighlighting = _syntaxService.GetHighlightingByName(value);
                OnPropertyChanged();
            }
        }
    }

    public string? FileExtension => string.IsNullOrEmpty(FilePath) ? null : Path.GetExtension(FilePath);

    public DateTime? LastKnownModified
    {
        get => _lastKnownModified;
        set
        {
            if (_lastKnownModified != value)
            {
                _lastKnownModified = value;
                _externalChangesAcknowledged = false;
                OnPropertyChanged();
            }
        }
    }

    public bool HasExternalChanges
    {
        get => _hasExternalChanges;
        private set
        {
            if (_hasExternalChanges != value)
            {
                _hasExternalChanges = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayTitle));
            }
        }
    }

    public void SetOriginalContent(string content)
    {
        _isContentLoaded = true;
        _originalContent = content;
        _content = content;
        OnPropertyChanged(nameof(Content));
        IsDirty = false;
        HasCachedChanges = false;
    }

    public void MarkAsSaved()
    {
        _originalContent = _content;
        IsDirty = false;
        HasCachedChanges = false;
        _cacheService.DeleteCache(Id);
    }

    public void LoadFromCache()
    {
        var cachedContent = _cacheService.LoadCache(Id);
        if (cachedContent != null)
        {
            _content = cachedContent;
            OnPropertyChanged(nameof(Content));
            IsDirty = true;
        }
    }

    public void EnsureContentLoaded()
    {
        if (_isContentLoaded) return;
        _isContentLoaded = true;

        // Load file content if path exists
        if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
        {
            try
            {
                var content = File.ReadAllText(_filePath);
                _originalContent = content;
                _content = content;
                _lastKnownModified = File.GetLastWriteTimeUtc(_filePath);
            }
            catch
            {
                // If file can't be read, leave content empty
            }
        }

        // Load cached content if available (overrides file content)
        var cachedContent = _cacheService.LoadCache(Id);
        if (cachedContent != null)
        {
            _content = cachedContent;
            _isDirty = true;
            _hasCachedChanges = false; // No longer just cached, now loaded
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(DisplayTitle));
        }

        OnPropertyChanged(nameof(Content));
    }

    public TabState ToState(int order)
    {
        return new TabState
        {
            Id = Id,
            Title = Title,
            FilePath = FilePath,
            Order = order,
            LastModified = _lastKnownModified,
            SyntaxName = _syntaxName
        };
    }

    public static TabViewModel FromState(TabState state, CacheService cacheService, SyntaxService syntaxService)
    {
        var vm = new TabViewModel(cacheService, syntaxService)
        {
            Id = state.Id,
            Title = state.Title,
            _filePath = state.FilePath,
            _isContentLoaded = false,
            _lastKnownModified = state.LastModified,
            // Check if there's cached changes without loading content
            _hasCachedChanges = cacheService.HasCache(state.Id)
        };

        // Restore saved syntax name, or auto-detect if not saved
        if (!string.IsNullOrEmpty(state.SyntaxName))
        {
            vm._syntaxName = state.SyntaxName;
            vm.SyntaxHighlighting = syntaxService.GetHighlightingByName(state.SyntaxName);
        }
        else
        {
            vm.UpdateSyntaxHighlighting();
        }

        return vm;
    }

    public bool CheckForExternalChanges()
    {
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            return false;

        if (_externalChangesAcknowledged)
            return false;

        try
        {
            var currentModified = File.GetLastWriteTimeUtc(_filePath);
            if (_lastKnownModified.HasValue && currentModified > _lastKnownModified.Value)
            {
                HasExternalChanges = true;
                return true;
            }
        }
        catch
        {
            // Ignore file access errors
        }

        return false;
    }

    public void AcknowledgeExternalChanges()
    {
        _externalChangesAcknowledged = true;
        HasExternalChanges = false;
    }

    public void ReloadFromDisk()
    {
        if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
            return;

        try
        {
            var content = File.ReadAllText(_filePath);
            _lastKnownModified = File.GetLastWriteTimeUtc(_filePath);
            _originalContent = content;
            _content = content;
            _externalChangesAcknowledged = false;
            HasExternalChanges = false;
            IsDirty = false;
            HasCachedChanges = false;
            _cacheService.DeleteCache(Id);
            OnPropertyChanged(nameof(Content));
        }
        catch
        {
            // Ignore file access errors
        }
    }

    private void UpdateDirtyState()
    {
        IsDirty = _content != _originalContent;
    }

    private void UpdateSyntaxHighlighting()
    {
        var syntaxName = _syntaxService.GetSyntaxNameForFile(FilePath) ?? "Plain Text";
        _syntaxName = syntaxName;
        SyntaxHighlighting = _syntaxService.GetHighlightingByName(syntaxName);
        OnPropertyChanged(nameof(SyntaxName));
    }

    public void RefreshSyntaxHighlighting()
    {
        // Re-fetch highlighting definition (useful after theme change)
        SyntaxHighlighting = _syntaxService.GetHighlightingByName(_syntaxName);
    }

    private void ScheduleCacheSave()
    {
        _cacheTimer.Stop();
        _cacheTimer.Start();
    }

    private void OnCacheTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (IsDirty)
        {
            _cacheService.SaveCache(Id, _content);
        }
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
