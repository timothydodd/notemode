using System;
using System.IO;
using System.Text.Json;
using Flit.Models;

namespace Flit.Services;

public class StateService
{
    private readonly string _stateFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public StateService()
    {
        var flitDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".flit"
        );
        Directory.CreateDirectory(flitDir);
        _stateFilePath = Path.Combine(flitDir, "state.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public AppState LoadState()
    {
        try
        {
            if (File.Exists(_stateFilePath))
            {
                var json = File.ReadAllText(_stateFilePath);
                return JsonSerializer.Deserialize<AppState>(json, _jsonOptions) ?? new AppState();
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
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception)
        {
            // Silently fail on save errors
        }
    }
}
