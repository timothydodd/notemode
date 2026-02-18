using System;
using System.Text.Json.Serialization;

namespace Flit.Models;

public class NoteState
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "Untitled";

    [JsonPropertyName("folderId")]
    public Guid? FolderId { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; set; }

    [JsonPropertyName("syntaxName")]
    public string? SyntaxName { get; set; }
}
