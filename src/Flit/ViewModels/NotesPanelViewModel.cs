using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Flit.ViewModels;

public class NotesPanelViewModel : INotifyPropertyChanged
{
    private readonly MainWindowViewModel _mainViewModel;

    public NotesPanelViewModel(MainWindowViewModel mainViewModel)
    {
        _mainViewModel = mainViewModel;
    }

    public ObservableCollection<NoteTreeItemViewModel> RootItems { get; } = new();

    public MainWindowViewModel MainViewModel => _mainViewModel;

    public void RefreshTree()
    {
        RootItems.Clear();

        var folders = _mainViewModel.NoteService.GetAllFolders();
        var notes = _mainViewModel.NoteService.GetAllNotes();

        // Build folder lookup
        var folderVms = folders.ToDictionary(f => f.Id, f => new NoteTreeItemViewModel
        {
            Id = f.Id,
            Name = f.Name,
            IsFolder = true,
            ParentFolderId = f.ParentId
        });

        // Assign folders to their parents
        foreach (var fvm in folderVms.Values.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
        {
            if (fvm.ParentFolderId.HasValue && folderVms.TryGetValue(fvm.ParentFolderId.Value, out var parent))
            {
                parent.Children.Add(fvm);
            }
            else
            {
                RootItems.Add(fvm);
            }
        }

        // Assign notes to their folders or root
        foreach (var note in notes.OrderBy(n => n.Title, StringComparer.OrdinalIgnoreCase))
        {
            var noteVm = new NoteTreeItemViewModel
            {
                Id = note.Id,
                Name = note.Title,
                IsFolder = false,
                ParentFolderId = note.FolderId,
                LastModified = note.LastModified,
                SyntaxName = note.SyntaxName
            };

            if (note.FolderId.HasValue && folderVms.TryGetValue(note.FolderId.Value, out var folder))
            {
                folder.Children.Add(noteVm);
            }
            else
            {
                RootItems.Add(noteVm);
            }
        }
    }

    public void MoveItem(NoteTreeItemViewModel item, NoteTreeItemViewModel? targetFolder)
    {
        var targetId = targetFolder?.Id;
        if (item.IsFolder)
        {
            _mainViewModel.NoteService.MoveFolder(item.Id, targetId);
        }
        else
        {
            _mainViewModel.NoteService.MoveNote(item.Id, targetId);
        }
        RefreshTree();
    }

    public void CreateFolder(Guid? parentId)
    {
        _mainViewModel.NoteService.CreateFolder("New Folder", parentId);
        RefreshTree();
    }

    public void DeleteItem(NoteTreeItemViewModel item)
    {
        if (item.IsFolder)
        {
            _mainViewModel.DeleteFolder(item.Id);
        }
        else
        {
            _mainViewModel.DeleteNote(item.Id);
        }
        RefreshTree();
    }

    public void RenameItem(Guid id, string newName, bool isFolder)
    {
        if (isFolder)
        {
            _mainViewModel.NoteService.RenameFolder(id, newName);
        }
        else
        {
            _mainViewModel.NoteService.RenameNote(id, newName);
            // Update open tab title if exists
            var openTab = _mainViewModel.Tabs.FirstOrDefault(t => t.Id == id);
            if (openTab != null)
            {
                openTab.Title = newName;
                _mainViewModel.SaveState();
            }
        }
        RefreshTree();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
