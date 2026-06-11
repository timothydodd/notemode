using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

#pragma warning disable CA1416 // Platform compatibility - all registry calls are guarded by IsWindows checks

namespace NoteMode.Services;

public class FileAssociationInfo
{
    public string Extension { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsAssociated { get; set; }
}

public class FileAssociationService
{
    private static readonly Dictionary<string, string[]> ExtensionCategories = new()
    {
        ["Text"] = new[] { ".txt", ".log", ".md", ".markdown", ".rst", ".csv", ".tsv" },
        ["Config"] = new[] { ".json", ".xml", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".env", ".properties" },
        ["Web"] = new[] { ".html", ".htm", ".css", ".scss", ".sass", ".less", ".svg" },
        ["Programming"] = new[] { ".cs", ".java", ".py", ".js", ".ts", ".jsx", ".tsx", ".c", ".cpp", ".h", ".hpp", ".go", ".rs", ".rb", ".php", ".swift", ".kt", ".scala", ".fs", ".fsx" },
        ["Script"] = new[] { ".sh", ".bash", ".ps1", ".psm1", ".bat", ".cmd", ".lua", ".pl", ".r" },
        ["Data"] = new[] { ".sql", ".graphql", ".proto" }
    };

    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Text"] = "text.ico",
        ["Config"] = "config.ico",
        ["Web"] = "html.ico",
        ["Programming"] = "csharp.ico",
        ["Script"] = "shell.ico",
        ["Data"] = "sql.ico",
    };

    private const string ProgIdPrefix = "NoteMode";
    private const string LegacyProgId = "NoteMode.Editor";

    private static readonly Dictionary<string, string> ExtensionToCategory =
        ExtensionCategories
            .SelectMany(kvp => kvp.Value.Select(ext => (ext, kvp.Key)))
            .ToDictionary(x => x.ext, x => x.Key);

    public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    public List<FileAssociationInfo> GetSupportedExtensions()
    {
        var result = new List<FileAssociationInfo>();

        foreach (var (category, extensions) in ExtensionCategories)
        {
            foreach (var ext in extensions)
            {
                result.Add(new FileAssociationInfo
                {
                    Extension = ext,
                    Category = category,
                    IsAssociated = IsWindows && CheckAssociation(ext)
                });
            }
        }

        return result;
    }

    public IEnumerable<string> GetCategories()
    {
        return ExtensionCategories.Keys;
    }

    public void SetAssociation(string extension)
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;
            if (!ExtensionToCategory.TryGetValue(extension, out var category)) return;

            var progId = ProgIdFor(category);
            var iconPath = ResolveIconPath(exePath, category);

            using (var progIdKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
            {
                progIdKey?.SetValue("", $"NoteMode {category} File");

                using var iconKey = progIdKey?.CreateSubKey("DefaultIcon");
                iconKey?.SetValue("", $"\"{iconPath}\"");

                using var commandKey = progIdKey?.CreateSubKey(@"shell\open\command");
                commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
            }

            using var extKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\{extension}");
            extKey?.SetValue("", progId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set association for {extension}: {ex.Message}");
        }
    }

    public void RemoveAssociation(string extension)
    {
        try
        {
            using var extKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}", writable: true);
            if (extKey != null)
            {
                var currentValue = extKey.GetValue("") as string;
                if (currentValue != null && IsOurProgId(currentValue))
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{extension}", throwOnMissingSubKey: false);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to remove association for {extension}: {ex.Message}");
        }
    }

    public void NotifyShell()
    {
        if (!IsWindows) return;

        try
        {
            SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);
        }
        catch (Exception)
        {
            // P/Invoke failed, shell will update eventually
        }
    }

    private bool CheckAssociation(string extension)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"Software\Classes\{extension}");
            return key?.GetValue("") is string value && IsOurProgId(value);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool IsOurProgId(string progId)
    {
        if (progId == LegacyProgId) return true;
        return ExtensionCategories.Keys.Any(c => progId == ProgIdFor(c));
    }

    private static string ProgIdFor(string category) => $"{ProgIdPrefix}.{category}";

    private static string ResolveIconPath(string exePath, string category)
    {
        var exeDir = Path.GetDirectoryName(exePath) ?? "";
        var iconFile = CategoryIcons[category];
        return Path.Combine(exeDir, "Assets", "FileIcons", iconFile);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
