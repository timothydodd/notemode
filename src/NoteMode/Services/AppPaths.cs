using System;
using System.IO;

namespace NoteMode.Services;

/// <summary>
/// Where NoteMode keeps its state, notes index and tab cache: <c>~/.notemode</c>, or the folder in
/// the <c>NOTEMODE_HOME</c> environment variable (used by the screenshot tool so it never touches
/// a real profile).
/// </summary>
public static class AppPaths
{
    public static string DataDir { get; } = ResolveDataDir();

    public static string CacheDir => Path.Combine(DataDir, "cache");

    private static string ResolveDataDir()
    {
        var overridden = Environment.GetEnvironmentVariable("NOTEMODE_HOME");
        return !string.IsNullOrWhiteSpace(overridden)
            ? overridden
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".notemode");
    }

    /// <summary>
    /// Writes a file via a temporary sibling and a rename, so a crash or power loss mid-write
    /// leaves either the old contents or the new ones, never a truncated file.
    /// </summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        // Unique per write: the cache timer and an explicit save can write the same file at once.
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try { File.Delete(temp); } catch { /* best effort */ }
            throw;
        }
    }
}
