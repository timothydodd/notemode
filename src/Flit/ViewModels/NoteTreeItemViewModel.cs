using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Flit.ViewModels;

public class NoteTreeItemViewModel : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isExpanded = true;
    private bool _isEditing;
    private string _editName = "";
    private bool _isDropTarget;

    public Guid Id { get; set; }

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsFolder { get; set; }

    public Guid? ParentFolderId { get; set; }

    public ObservableCollection<NoteTreeItemViewModel> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing != value)
            {
                _isEditing = value;
                OnPropertyChanged();
            }
        }
    }

    public string EditName
    {
        get => _editName;
        set
        {
            if (_editName != value)
            {
                _editName = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            if (_isDropTarget != value)
            {
                _isDropTarget = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime? LastModified { get; set; }

    public string? SyntaxName { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
