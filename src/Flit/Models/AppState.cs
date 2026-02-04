using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Flit.Models;

public class AppState
{
    [JsonPropertyName("tabs")]
    public List<TabState> Tabs { get; set; } = new();

    [JsonPropertyName("activeTabId")]
    public Guid? ActiveTabId { get; set; }

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 1200;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 800;

    [JsonPropertyName("fontSize")]
    public double FontSize { get; set; } = 10;

    [JsonPropertyName("showWhitespace")]
    public bool ShowWhitespace { get; set; } = false;

    [JsonPropertyName("showLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;

    [JsonPropertyName("useLightTheme")]
    public bool UseLightTheme { get; set; }
}
