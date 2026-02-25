using System;
using System.IO;

namespace NoteMode.Services;

public class CacheService
{
    private readonly string _cacheDir;

    public CacheService()
    {
        _cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".notemode",
            "cache"
        );
        Directory.CreateDirectory(_cacheDir);
    }

    public string GetCachePath(Guid tabId)
    {
        return Path.Combine(_cacheDir, $"{tabId}.cache");
    }

    public void SaveCache(Guid tabId, string content)
    {
        try
        {
            var path = GetCachePath(tabId);
            File.WriteAllText(path, content);
        }
        catch (Exception)
        {
            // Silently fail on cache errors
        }
    }

    public string? LoadCache(Guid tabId)
    {
        try
        {
            var path = GetCachePath(tabId);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch (Exception)
        {
            // Return null if loading fails
        }

        return null;
    }

    public void DeleteCache(Guid tabId)
    {
        try
        {
            var path = GetCachePath(tabId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // Silently fail on delete errors
        }
    }

    public bool HasCache(Guid tabId)
    {
        return File.Exists(GetCachePath(tabId));
    }
}
