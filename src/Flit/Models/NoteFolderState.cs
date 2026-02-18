using System;
using System.Text.Json.Serialization;

namespace Flit.Models;

public class NoteFolderState
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "New Folder";

    [JsonPropertyName("parentId")]
    public Guid? ParentId { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }
}
