using System;
using System.IO;
using System.Threading;

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
    /// Checks that files can be created in the data and cache folders. Every save goes through a
    /// temporary sibling file, so a folder that is readable but not writable (e.g. copied from
    /// another machine as root) would otherwise lose notes, cached edits and state without a word.
    /// Returns the reason when a folder is not writable, otherwise null.
    /// </summary>
    public static string? GetDataDirProblem()
    {
        foreach (var dir in new[] { DataDir, CacheDir })
        {
            var probe = Path.Combine(dir, $".write-test.{Guid.NewGuid():N}.tmp");
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
            }
            catch (Exception ex)
            {
                try { File.Delete(probe); } catch { /* best effort */ }
                return $"{dir}: {ex.Message}";
            }
        }

        return null;
    }

    /// <summary>
    /// Writes a file via a temporary sibling and a rename, so a crash or power loss mid-write
    /// leaves either the old contents or the new ones, never a truncated file.
    /// </summary>
    public static void WriteAllTextAtomic(string path, string contents)
    {
        try
        {
            WriteAtomic(path, contents);
            Interlocked.Exchange(ref _writesFailing, 0);
        }
        catch (Exception ex)
        {
            // Raised once per run of failures (the auto-cache retries every keystroke), from
            // whichever thread was writing. Callers still see the exception.
            if (Interlocked.Exchange(ref _writesFailing, 1) == 0)
                WriteFailed?.Invoke(path, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Raised (path, reason) when NoteMode's own data (state, notes index, tab cache) stops being
    /// writable, and again only after a write has succeeded in between. The services swallow the
    /// exception so the editor keeps working; this is how the user still gets told.
    /// </summary>
    public static event Action<string, string>? WriteFailed;

    private static int _writesFailing;

    private static void WriteAtomic(string path, string contents)
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
