using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Flit.Models;

public class NotesIndex
{
    [JsonPropertyName("notes")]
    public List<NoteState> Notes { get; set; } = new();

    [JsonPropertyName("folders")]
    public List<NoteFolderState> Folders { get; set; } = new();
}
