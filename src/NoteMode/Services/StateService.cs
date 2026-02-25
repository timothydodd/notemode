using System;
using System.IO;
using System.Text.Json;
using NoteMode.Models;

namespace NoteMode.Services;

public class StateService
{
    private readonly string _stateFilePath;

    public StateService()
    {
        var appDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".notemode"
        );
        Directory.CreateDirectory(appDir);
        _stateFilePath = Path.Combine(appDir, "state.json");
    }

    public AppState LoadState()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = File.ReadAllText(_stateFilePath);
                return JsonSerializer.Deserialize(json, AppJsonContext.Default.AppState) ?? new AppState();
            }
        }
        catch (Exception)
        {
            // If loading fails, return empty state
        }

        return new AppState();
    }

    public void SaveState(AppState state)
    {
        try
        {
            var json = JsonSerializer.Serialize(state, AppJsonContext.Default.AppState);
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail on save errors
        }
    }
}
