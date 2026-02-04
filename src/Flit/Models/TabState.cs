using System;
using System.Text.Json.Serialization;

namespace Flit.Models;

public class TabState
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "Untitled";

    [JsonPropertyName("filePath")]
    public string? FilePath { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("lastModified")]
    public DateTime? LastModified { get; set; }
}
