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
    private static readonly Color LightForeground = Color.Parse("#000000");
    private static readonly Color LightComment = Color.Parse("#008000");
    private static readonly Color LightString = Color.Parse("#a31515");
    private static readonly Color LightKeyword = Color.Parse("#0000ff");
    private static readonly Color LightType = Color.Parse("#267f99");
    private static readonly Color LightFunction = Color.Parse("#795e26");
    private static readonly Color LightNumber = Color.Parse("#098658");
    private static readonly Color LightVariable = Color.Parse("#001080");
    private static readonly Color LightOperator = Color.Parse("#000000");
    private static readonly Color LightPreprocessor = Color.Parse("#0000ff");
    private static readonly Color LightAttribute = Color.Parse("#267f99");
    private static readonly Color LightTag = Color.Parse("#800000");
    private static readonly Color LightAttributeName = Color.Parse("#ff0000");
    private static readonly Color LightAttributeValue = Color.Parse("#0000ff");
    private static readonly Color LightError = Color.Parse("#ff0000");
    private static readonly Color LightLink = Color.Parse("#0066cc");

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

        // Apply to main rule set and all nested rule sets
        if (definition.MainRuleSet != null)
        {
            var visited = new HashSet<HighlightingRuleSet>();
            ApplyThemeColorsToRuleSet(definition.MainRuleSet, visited);
        }
    }

    private void ApplyThemeColorsToRuleSet(HighlightingRuleSet ruleSet, HashSet<HighlightingRuleSet> visited)
    {
        if (!visited.Add(ruleSet))
            return;
        foreach (var rule in ruleSet.Rules)
        {
            if (rule.Color != null)
            {
                ApplyThemeColor(rule.Color);
            }
        }

        foreach (var span in ruleSet.Spans)
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

            // Recursively process nested rule sets within spans
            if (span.RuleSet != null)
            {
                ApplyThemeColorsToRuleSet(span.RuleSet, visited);
            }
        }
    }

    private void ApplyThemeColor(HighlightingColor color)
    {
        var name = color.Name?.ToLowerInvariant() ?? "";

        if (_useLightTheme)
        {
            ApplyLightThemeColor(color, name);
        }
        else
        {
            ApplyDarkThemeColor(color, name);
        }
    }

    private void ApplyLightThemeColor(HighlightingColor color, string name)
    {
        // VS Code Light+ inspired colors
        if (name.Contains("comment"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightComment);
            color.FontStyle = FontStyle.Italic;
        }
        else if (name.Contains("string") || name.Contains("char"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightString);
        }
        else if (name.Contains("keyword") || name.Contains("visibility") || name.Contains("modifier") || name.Contains("access"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightKeyword);
        }
        else if (name.Contains("number") || name.Contains("digit"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightNumber);
        }
        else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface") || name.Contains("enum"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightType);
        }
        else if (name.Contains("method") || name.Contains("function"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightFunction);
        }
        else if (name.Contains("variable") || name.Contains("parameter") || name.Contains("field"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightVariable);
        }
        else if (name.Contains("operator") || name.Contains("punctuation"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightOperator);
        }
        else if (name.Contains("preprocessor") || name.Contains("directive"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightPreprocessor);
        }
        else if (name.Contains("namespace") || name.Contains("using"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightForeground);
        }
        else if (name.Contains("constant") || name.Contains("bool"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightKeyword);
        }
        else if (name.Contains("tag") || name.Contains("element"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightTag);
        }
        else if (name.Contains("attributename"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightAttributeName);
        }
        else if (name.Contains("attributevalue"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightAttributeValue);
        }
        else if (name.Contains("attribute"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightAttribute);
        }
        else if (name.Contains("entity") || name.Contains("escape"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightKeyword);
        }
        else if (name.Contains("error") || name.Contains("invalid"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightError);
        }
        else if (name.Contains("link") || name.Contains("hyperlink") || name.Contains("url") || name.Contains("uri"))
        {
            color.Foreground = new SimpleHighlightingBrush(LightLink);
            color.Underline = true;
        }
        else
        {
            color.Foreground = new SimpleHighlightingBrush(LightForeground);
        }
    }

    private void ApplyDarkThemeColor(HighlightingColor color, string name)
    {
        // Dracula theme colors
        if (name.Contains("comment"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaComment);
            color.FontStyle = FontStyle.Italic;
        }
        else if (name.Contains("string") || name.Contains("char"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaYellow);
        }
        else if (name.Contains("keyword") || name.Contains("visibility") || name.Contains("modifier") || name.Contains("access"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPink);
        }
        else if (name.Contains("number") || name.Contains("digit"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPurple);
        }
        else if (name.Contains("type") || name.Contains("class") || name.Contains("struct") || name.Contains("interface") || name.Contains("enum"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaCyan);
        }
        else if (name.Contains("method") || name.Contains("function"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaGreen);
        }
        else if (name.Contains("variable") || name.Contains("parameter") || name.Contains("field"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaOrange);
        }
        else if (name.Contains("operator") || name.Contains("punctuation"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPink);
        }
        else if (name.Contains("preprocessor") || name.Contains("directive"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPink);
        }
        else if (name.Contains("namespace") || name.Contains("using"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPink);
        }
        else if (name.Contains("constant") || name.Contains("bool"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPurple);
        }
        else if (name.Contains("tag") || name.Contains("element"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPink);
        }
        else if (name.Contains("attributename"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaGreen);
        }
        else if (name.Contains("attributevalue"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaYellow);
        }
        else if (name.Contains("attribute"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaGreen);
        }
        else if (name.Contains("entity") || name.Contains("escape"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaPurple);
        }
        else if (name.Contains("error") || name.Contains("invalid"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaRed);
        }
        else if (name.Contains("link") || name.Contains("hyperlink") || name.Contains("url") || name.Contains("uri"))
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaLink);
            color.Underline = true;
        }
        else
        {
            color.Foreground = new SimpleHighlightingBrush(DraculaForeground);
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
