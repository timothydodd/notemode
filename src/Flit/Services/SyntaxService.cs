using Avalonia.Media;
using AvaloniaEdit.Highlighting;

namespace Flit.Services;

public class LanguageInfo
{
    public string Name { get; set; } = "";
    public string[] Extensions { get; set; } = Array.Empty<string>();
}

public class SyntaxService
{
    // Dracula theme colors (dark theme)
    private static readonly Color DraculaForeground = Color.Parse("#f8f8f2");
    private static readonly Color DraculaComment = Color.Parse("#6272a4");
    private static readonly Color DraculaCyan = Color.Parse("#8be9fd");
    private static readonly Color DraculaGreen = Color.Parse("#50fa7b");
    private static readonly Color DraculaOrange = Color.Parse("#ffb86c");
    private static readonly Color DraculaPink = Color.Parse("#ff79c6");
    private static readonly Color DraculaPurple = Color.Parse("#bd93f9");
    private static readonly Color DraculaRed = Color.Parse("#ff5555");
    private static readonly Color DraculaYellow = Color.Parse("#f1fa8c");
    private static readonly Color DraculaLink = Color.Parse("#8be9fd");

    // Light theme colors (VS Code Light+ inspired)
    private static readonly Color LightForeground = Color.Parse("#1e1e1e");
    private static readonly Color LightComment = Color.Parse("#008000");      // Green comments
    private static readonly Color LightCyan = Color.Parse("#267f99");         // Teal for types/classes
    private static readonly Color LightGreen = Color.Parse("#795e26");        // Brown for functions
    private static readonly Color LightOrange = Color.Parse("#e65100");       // Orange for parameters/variables
    private static readonly Color LightPink = Color.Parse("#af00db");         // Purple for keywords
    private static readonly Color LightPurple = Color.Parse("#0000ff");       // Blue for keywords
    private static readonly Color LightRed = Color.Parse("#a31515");          // Red for errors
    private static readonly Color LightYellow = Color.Parse("#a31515");       // Brown/red for strings
    private static readonly Color LightLink = Color.Parse("#0066cc");         // Blue for links
    private static readonly Color LightNumber = Color.Parse("#098658");       // Green for numbers

    private bool _useLightTheme;

    private readonly Dictionary<string, string> _extensionToSyntax = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".cs", "C#" },
        { ".csx", "C#" },
        { ".js", "JavaScript" },
        { ".ts", "JavaScript" },
        { ".jsx", "JavaScript" },
        { ".tsx", "JavaScript" },
        { ".json", "Json" },
        { ".xml", "XML" },
        { ".xaml", "XML" },
        { ".axaml", "XML" },
        { ".html", "HTML" },
        { ".htm", "HTML" },
        { ".css", "CSS" },
        { ".py", "Python" },
        { ".java", "Java" },
        { ".cpp", "C++" },
        { ".c", "C++" },
        { ".h", "C++" },
        { ".hpp", "C++" },
        { ".sql", "TSQL" },
        { ".md", "MarkDown" },
        { ".markdown", "MarkDown" },
        { ".php", "PHP" },
        { ".vb", "VB" },
        { ".ps1", "PowerShell" },
        { ".psm1", "PowerShell" },
        { ".sh", "Shell" },
        { ".bash", "Shell" },
        { ".yaml", "YAML" },
        { ".yml", "YAML" }
    };

    private readonly Dictionary<string, IHighlightingDefinition> _cachedDefinitions = new();

    public void SetLightTheme(bool useLightTheme)
    {
        if (_useLightTheme != useLightTheme)
        {
            _useLightTheme = useLightTheme;
            // Clear cached definitions to force re-application of colors
            _cachedDefinitions.Clear();
        }
    }

    public bool IsLightTheme => _useLightTheme;

    public IHighlightingDefinition? GetHighlighting(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        var extension = Path.GetExtension(filePath);

        // No highlighting for .txt or empty extension
        if (string.IsNullOrEmpty(extension) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string? syntaxName = null;
        if (_extensionToSyntax.TryGetValue(extension, out var name))
        {
            syntaxName = name;
        }
        else
        {
            var def = HighlightingManager.Instance.GetDefinitionByExtension(extension);
            if (def != null)
            {
                syntaxName = def.Name;
            }
        }

        if (syntaxName == null)
        {
            return null;
        }

        // Return cached themed definition if available
        if (_cachedDefinitions.TryGetValue(syntaxName, out var cachedDef))
        {
            return cachedDef;
        }

        var definition = HighlightingManager.Instance.GetDefinition(syntaxName);
        if (definition != null)
        {
            ApplyThemeColors(definition);
            _cachedDefinitions[syntaxName] = definition;
        }

        return definition;
    }

    private void ApplyThemeColors(IHighlightingDefinition definition)
    {
        foreach (var color in definition.NamedHighlightingColors)
        {
            ApplyThemeColor(color);
        }

        // Also apply to main rule set colors
        if (definition.MainRuleSet != null)
        {
            foreach (var rule in definition.MainRuleSet.Rules)
            {
                if (rule.Color != null)
                {
                    ApplyThemeColor(rule.Color);
                }
            }

            foreach (var span in definition.MainRuleSet.Spans)
            {
                if (span.SpanColor != null)
                {
                    ApplyThemeColor(span.SpanColor);
                }
                if (span.StartColor != null)
                {
                    ApplyThemeColor(span.StartColor);
                }
                if (span.EndColor != null)
                {
                    ApplyThemeColor(span.EndColor);
                }
            }
        }
    }

    private void ApplyThemeColor(HighlightingColor color)
    {
        var name = color.Name?.ToLowerInvariant() ?? "";

        // Select colors based on current theme
        var foreground = _useLightTheme ? LightForeground : DraculaForeground;
        var comment = _useLightTheme ? LightComment : DraculaComment;
        var cyan = _useLightTheme ? LightCyan : DraculaCyan;
        var green = _useLightTheme ? LightGreen : DraculaGreen;
        var orange = _useLightTheme ? LightOrange : DraculaOrange;
        var pink = _useLightTheme ? LightPink : DraculaPink;
        var purple = _useLightTheme ? LightPurple : DraculaPurple;
        var red = _useLightTheme ? LightRed : DraculaRed;
        var yellow = _useLightTheme ? LightYellow : DraculaYellow;
        var link = _useLightTheme ? LightLink : DraculaLink;
        var number = _useLightTheme ? LightNumber : DraculaPurple;

        // Map common highlighting names to theme colors
        if (name.Contains("comment"))
        {
            color.Foreground = new SimpleHighlightingBrush(comment);
            color.FontStyle = FontStyle.Italic;
        }
        else if (name.Contains("string") || name.Contains("char"))
        {
            color.Foreground = new SimpleHighlightingBrush(yellow);
        }
        else if (name.Contains("keyword") || name.Contains("keywords"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("number") || name.Contains("digit"))
        {
            color.Foreground = new SimpleHighlightingBrush(number);
        }
        else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface") || name.Contains("enum"))
        {
            color.Foreground = new SimpleHighlightingBrush(cyan);
        }
        else if (name.Contains("method") || name.Contains("function"))
        {
            color.Foreground = new SimpleHighlightingBrush(green);
        }
        else if (name.Contains("operator") || name.Contains("punctuation"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("preprocessor") || name.Contains("directive"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("attribute"))
        {
            color.Foreground = new SimpleHighlightingBrush(green);
        }
        else if (name.Contains("namespace") || name.Contains("using"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("visibility") || name.Contains("modifier") || name.Contains("access"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("variable") || name.Contains("parameter"))
        {
            color.Foreground = new SimpleHighlightingBrush(orange);
        }
        else if (name.Contains("constant") || name.Contains("bool"))
        {
            color.Foreground = new SimpleHighlightingBrush(purple);
        }
        else if (name.Contains("tag") || name.Contains("element"))
        {
            color.Foreground = new SimpleHighlightingBrush(pink);
        }
        else if (name.Contains("attributename"))
        {
            color.Foreground = new SimpleHighlightingBrush(green);
        }
        else if (name.Contains("attributevalue"))
        {
            color.Foreground = new SimpleHighlightingBrush(yellow);
        }
        else if (name.Contains("entity") || name.Contains("escape"))
        {
            color.Foreground = new SimpleHighlightingBrush(purple);
        }
        else if (name.Contains("error") || name.Contains("invalid"))
        {
            color.Foreground = new SimpleHighlightingBrush(red);
        }
        else if (name.Contains("link") || name.Contains("hyperlink") || name.Contains("url") || name.Contains("uri"))
        {
            color.Foreground = new SimpleHighlightingBrush(link);
            color.Underline = true;
        }
        else
        {
            // Default foreground for any unmatched colors
            color.Foreground = new SimpleHighlightingBrush(foreground);
        }
    }

    public string? GetSyntaxNameForExtension(string? extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        if (!extension.StartsWith("."))
        {
            extension = "." + extension;
        }

        if (_extensionToSyntax.TryGetValue(extension, out var syntaxName))
        {
            return syntaxName;
        }

        return null;
    }

    public string? GetSyntaxNameForFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension) || extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return "Plain Text";

        if (_extensionToSyntax.TryGetValue(extension, out var name))
            return name;

        var def = HighlightingManager.Instance.GetDefinitionByExtension(extension);
        return def?.Name ?? "Plain Text";
    }

    public IHighlightingDefinition? GetHighlightingByName(string? syntaxName)
    {
        if (string.IsNullOrEmpty(syntaxName) || syntaxName == "Plain Text")
            return null;

        if (_cachedDefinitions.TryGetValue(syntaxName, out var cachedDef))
            return cachedDef;

        var definition = HighlightingManager.Instance.GetDefinition(syntaxName);
        if (definition != null)
        {
            ApplyThemeColors(definition);
            _cachedDefinitions[syntaxName] = definition;
        }

        return definition;
    }

    public IEnumerable<LanguageInfo> GetAllLanguages()
    {
        var languages = new List<LanguageInfo>
        {
            new LanguageInfo { Name = "Plain Text", Extensions = new[] { ".txt" } }
        };

        // Add languages from our extension map
        var grouped = _extensionToSyntax
            .GroupBy(kvp => kvp.Value)
            .ToDictionary(g => g.Key, g => g.Select(kvp => kvp.Key).ToArray());

        foreach (var kvp in grouped)
        {
            languages.Add(new LanguageInfo { Name = kvp.Key, Extensions = kvp.Value });
        }

        // Add languages from HighlightingManager that we don't already have
        foreach (var def in HighlightingManager.Instance.HighlightingDefinitions)
        {
            if (!languages.Any(l => l.Name.Equals(def.Name, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    languages.Add(new LanguageInfo
                    {
                        Name = def.Name,
                        Extensions = def.Properties.TryGetValue("Extensions", out var ext)
                            ? ext.Split(';')
                            : Array.Empty<string>()
                    });
                }
                catch
                {
                    // Ignore any errors
                }
            }
        }

        return languages.OrderBy(l => l.Name == "Plain Text" ? "" : l.Name);
    }
}
