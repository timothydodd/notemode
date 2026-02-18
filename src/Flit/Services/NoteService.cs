using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Flit.Models;

namespace Flit.Services;

public class NoteService
{
    private readonly string _indexPath;
    private NotesIndex _index;

    public NoteService()
    {
        var flitDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".flit"
        );
        Directory.CreateDirectory(flitDir);
        _indexPath = Path.Combine(flitDir, "notes.json");
        _index = LoadIndex();
    }

    private NotesIndex LoadIndex()
    {
        try
        {
            if (File.Exists(_indexPath))
            {
                var json = File.ReadAllText(_indexPath);
                return JsonSerializer.Deserialize(json, AppJsonContext.Default.NotesIndex) ?? new NotesIndex();
            }
        }
        catch
        {
            // If loading fails, return empty index
        }

        return new NotesIndex();
    }

    private void SaveIndex()
    {
        try
        {
            var json = JsonSerializer.Serialize(_index, AppJsonContext.Default.NotesIndex);
            File.WriteAllText(_indexPath, json);
        }
        catch
        {
            // Silently fail on save errors
        }
    }

    public NoteState CreateNote(Guid id, string title, string? syntaxName)
    {
        var note = new NoteState
        {
            Id = id,
            Title = title,
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow,
            SyntaxName = syntaxName
        };
        _index.Notes.Add(note);
        SaveIndex();
        return note;
    }

    public void DeleteNote(Guid id)
    {
        _index.Notes.RemoveAll(n => n.Id == id);
        SaveIndex();
    }

    public NoteState? GetNote(Guid id)
    {
        return _index.Notes.FirstOrDefault(n => n.Id == id);
    }

    public List<NoteState> GetAllNotes()
    {
        return _index.Notes.ToList();
    }

    public void UpdateNote(Guid id, string title, string? syntaxName)
    {
        var note = _index.Notes.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            note.Title = title;
            note.SyntaxName = syntaxName;
            note.LastModified = DateTime.UtcNow;
            SaveIndex();
        }
    }

    public NoteFolderState CreateFolder(string name, Guid? parentId)
    {
        var folder = new NoteFolderState
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentId = parentId,
            Order = _index.Folders.Count
        };
        _index.Folders.Add(folder);
        SaveIndex();
        return folder;
    }

    public List<Guid> DeleteFolder(Guid id)
    {
        var deletedNoteIds = new List<Guid>();
        DeleteFolderRecursive(id, deletedNoteIds);
        SaveIndex();
        return deletedNoteIds;
    }

    private void DeleteFolderRecursive(Guid folderId, List<Guid> deletedNoteIds)
    {
        // Find and delete child folders first
        var childFolders = _index.Folders.Where(f => f.ParentId == folderId).ToList();
        foreach (var child in childFolders)
        {
            DeleteFolderRecursive(child.Id, deletedNoteIds);
        }

        // Collect note IDs in this folder
        var notesInFolder = _index.Notes.Where(n => n.FolderId == folderId).ToList();
        foreach (var note in notesInFolder)
        {
            deletedNoteIds.Add(note.Id);
        }

        // Remove notes and the folder
        _index.Notes.RemoveAll(n => n.FolderId == folderId);
        _index.Folders.RemoveAll(f => f.Id == folderId);
    }

    public void RenameFolder(Guid id, string name)
    {
        var folder = _index.Folders.FirstOrDefault(f => f.Id == id);
        if (folder != null)
        {
            folder.Name = name;
            SaveIndex();
        }
    }

    public List<NoteFolderState> GetAllFolders()
    {
        return _index.Folders.ToList();
    }

    public void MoveNote(Guid noteId, Guid? folderId)
    {
        var note = _index.Notes.FirstOrDefault(n => n.Id == noteId);
        if (note != null)
        {
            note.FolderId = folderId;
            SaveIndex();
        }
    }

    public void MoveFolder(Guid folderId, Guid? newParentId)
    {
        // Prevent cycle: newParentId can't be folderId or any descendant of folderId
        if (newParentId.HasValue && IsDescendant(newParentId.Value, folderId))
            return;

        var folder = _index.Folders.FirstOrDefault(f => f.Id == folderId);
        if (folder != null)
        {
            folder.ParentId = newParentId;
            SaveIndex();
        }
    }

    private bool IsDescendant(Guid candidateId, Guid ancestorId)
    {
        if (candidateId == ancestorId) return true;

        var current = _index.Folders.FirstOrDefault(f => f.Id == candidateId);
        while (current?.ParentId != null)
        {
            if (current.ParentId == ancestorId) return true;
            current = _index.Folders.FirstOrDefault(f => f.Id == current.ParentId);
        }

        return false;
    }

    public bool IsNote(Guid id)
    {
        return _index.Notes.Any(n => n.Id == id);
    }

    public void RenameNote(Guid id, string newTitle)
    {
        var note = _index.Notes.FirstOrDefault(n => n.Id == id);
        if (note != null)
        {
            note.Title = newTitle;
            note.LastModified = DateTime.UtcNow;
            SaveIndex();
        }
    }
}
