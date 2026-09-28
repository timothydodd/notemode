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
        Directory.CreateDirectory(AppPaths.DataDir);
        _stateFilePath = Path.Combine(AppPaths.DataDir, "state.json");
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
            AppPaths.WriteAllTextAtomic(_stateFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail on save errors
        }
    }
}
