using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoteMode.ViewModels;

public class FileTreeItemViewModel : INotifyPropertyChanged
{
    private bool _isExpanded;
    private bool _childrenLoaded;

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public ObservableCollection<FileTreeItemViewModel> Children { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public FileTreeItemViewModel(string name, string fullPath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;

        if (isDirectory)
        {
            // Add a sentinel child so the expand arrow shows
            Children.Add(new FileTreeItemViewModel("Loading...", "", false));
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();

                if (value && IsDirectory && !_childrenLoaded)
                {
                    ExpandRequested?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    public bool ChildrenLoaded
    {
        get => _childrenLoaded;
        set => _childrenLoaded = value;
    }

    public event EventHandler? ExpandRequested;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
